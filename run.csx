#load "JsonListUti.csx"
#load "JsonObjectUti.csx"

using System;
using System.Text;
using Jayrock.Json;
using Jayrock.Json.Conversion;
using System.IO;

using Microsoft.Azure;
using Microsoft.WindowsAzure; // Namespace for CloudConfigurationManager
using Microsoft.WindowsAzure.Storage; // Namespace for CloudStorageAccount
using Microsoft.WindowsAzure.Storage.Blob; // Namespace for Blob storage types

static bool DEBUG_FUNCTION = true;

public static void Run(string myEventHubMessage, TraceWriter log)
{
    // In this context, myEventHubMessage is already a string by itself
    if (myEventHubMessage != "")
    {
        // process the hubData if it is not null.
        // string receivedChunk = Encoding.UTF8.GetString(myEventHubMessage.GetBytes());
        string receivedChunk = myEventHubMessage;

        // Currently, the format of the receivedChunk is expected to be:
        /*
        * {
        *    "devId": (string)<some-id>,
        *    "floatNo": (float)<some-fp>
        * }
        */
        // Extract deviceId and floatNo from chunk
        string deviceId = "";
        string floatNo = "";

        // Check if the payload is legit:
        // By our standard, devId and floatNo are compulsory
        if (JsonObjectUti.GetTarget("devId", receivedChunk, ref deviceId) &&
            JsonObjectUti.GetTarget("floatNo", receivedChunk, ref floatNo))
        {
            // Currently, we consider floating point only
            // Register device into the list
            InsertDevToList(deviceId, "fp-container");

            // Passing on the whole chunk to StoreFp()
            StoreFp(receivedChunk);
        }

        if (DEBUG_FUNCTION)
        {
            log.Info($"Message Detected.\n");
            log.Info($"Device Id: {deviceId}\n");
            log.Info($"Float No.: {floatNo}\n");

            string timeStamp = "";
            if (JsonObjectUti.GetTarget("timeStamp", receivedChunk, ref timeStamp))
            {
                log.Info($"timeStamp: {timeStamp}\n");
            }

            string seqNo = "";
            if (JsonObjectUti.GetTarget("seqNo", receivedChunk, ref seqNo))
            {
                log.Info($"seqNo: {seqNo}\n");
            }
            log.Info("\n");
        }
    }
}

static void InsertDevToList(string devId, string containerName)
{
    string blobName = "device-list.json";
    string rcvChunk = DownloadBlob(containerName, blobName);
    string sentChunk = "";

    List<string> devList = new List<string>();

    if (!JsonListUti.SplitList(rcvChunk, ref devList))
    {
        // If split list fail, that means the rcvChunk is empty.
        // Initialize devList into an empty array
        devList.Clear();
    }

    // Iterate through the list, see if a match is found.
    bool match_found = false;
    foreach (var elem in devList)
    {
        if (elem == devId)
        {
            match_found = true;
            break;
        }
    }

    if (!match_found)
    {
        // Add the element into the list, if match not found.
                devList.Add(devId);

        // Stringify the data structure into the json string
        using (JsonWriter workbench = new JsonTextWriter())
        {
            workbench.WriteStartArray();
            foreach (var elem in devList)
            {
                workbench.WriteString(elem);
            }

            workbench.WriteEndArray();
            sentChunk = workbench.ToString();
        }

        // Upload the chunk into ./fp-container/device-list.json
        // The container name doesn't necessarily need to be fp-container
        // The method can be generally applied to any other container
        UploadBlob(sentChunk, containerName, blobName);
    }
}

static string DownloadBlob(string containerName, string blobName)
{
    // Get reference to the blob specified.
    CloudBlockBlob blobManaged = GetBlobRef(containerName, blobName);
            
    string chunk = "";
    if (blobManaged.Exists())
    {
        using (var buf = new MemoryStream())
        {
            // Download and store in buffer.
            blobManaged.DownloadToStream(buf);

            // load from buffer to string
            chunk = Encoding.UTF8.GetString(buf.ToArray());
        }

        // Strip down the definition of the callbacks
        if (blobName == "device-list.json")
        {
            chunk = chunk.TrimStart("UpdateList(".ToArray<char>());
            chunk = chunk.TrimEnd(")".ToArray<char>());
        }
        else if (blobName.EndsWith(".json"))
        {
            chunk = chunk.TrimStart("DisplayDevice(".ToArray<char>());
            chunk = chunk.TrimEnd(")".ToArray<char>());
        }
    }
                               
    return chunk;
}

static CloudBlockBlob GetBlobRef(string containerName, string blobName)
{
    // "StorageConnectionString" is juz a key, with the key the config
    // manager should be able to hash and retrieve a legit connection string
    // (from a config file)
    CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
        CloudConfigurationManager.GetSetting("StorageConnectionString"));

    CloudBlobClient storageClient = storageAccount.CreateCloudBlobClient();

    CloudBlobContainer blobContainer = storageClient.GetContainerReference(containerName);

    if (!blobContainer.Exists())
    {
        blobContainer.Create();
        blobContainer.SetPermissions(
        new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });
    }

    // Get reference to the blob specified.
    return blobContainer.GetBlockBlobReference(blobName);
}

static void UploadBlob(string i_chunk, string containerName, string blobName)
{
    // Wrap the data-structure with the definition of the callback
    if (blobName == "device-list.json")
    {
        i_chunk = "UpdateList(" + i_chunk + ")";
    }
    else if (blobName.EndsWith(".json"))
    {
        // Wrap the data-structure only if it ends with suffix ".json"
        i_chunk = "DisplayDevice(" + i_chunk + ")";
    }

    GetBlobRef(containerName, blobName).UploadText(i_chunk);
}

static void StoreFp(string i_chunk)
{
    string devId = "";
    JsonObjectUti.GetTarget("devId", i_chunk, ref devId);
    string blobName = devId + ".json";
    string containerName = "fp-container";
    string dwnld_chunk = DownloadBlob(containerName, blobName);
    string toBeStored = "";

    if (dwnld_chunk == "")
    {
        // Initialize the blob
        // toBeStored = InitializeFpBlob(devId, floatNo);
        // In the new standard, we pass the whole chunk
        // Extraction is done within the method
        toBeStored = InitializeFpBlob(i_chunk);
    }
    else
    {
        // Update the existing blob
        // toBeStored = UpdateFpBlob(chunk, devId, floatNo);
        toBeStored = UpdateFpBlob(dwnld_chunk, i_chunk);
    }

    UploadBlob(toBeStored, containerName, blobName);

    // Deal with the CSV part of the storage
    string csvBlobName = devId + ".csv"; // Container name remains the same
    string csvToBeStored = "Count, TimeStamp, Data\n";

    // Extraction gone here
    // Extract buffer first
    string bufferChunk = "";
    JsonObjectUti.GetTarget("buffer", toBeStored, ref bufferChunk);
    // Extract timeStamps second
    // but timeStamps might not exist
    string timeStampsChunk = "";
    if (!JsonObjectUti.GetTarget("timeStamps", toBeStored, ref timeStampsChunk))
    {
        // Empty string indicates timeStamps doesn't exist
        timeStampsChunk = "";
    }

    // Splitting the bufferChunk into real list with tokens
    List<string> bufferList = new List<string>();
    JsonListUti.SplitList(bufferChunk, ref bufferList);

    // Splitting the timeStamps into real list with tokens
    List<string> timeStampsList = new List<string>();
    JsonListUti.SplitList(timeStampsChunk, ref timeStampsList);

    for (int i = 0;i < timeStampsList.Count; i++)
    {
        // Further split each particular timeStamp
        List<string> timeStampPair = new List<string>();
        JsonListUti.SplitList(timeStampsList[i], ref timeStampPair);

        csvToBeStored += (Convert.ToString(i) + "," + timeStampPair[0] + "," + bufferList[i] + "\n");
    }

    UploadBlob(csvToBeStored, containerName, csvBlobName);
}

static string InitializeFpBlob(string i_chunk)
{
    // Extraction done here
    string devId = "";
    string floatNo = "";

    JsonObjectUti.GetTarget("devId", i_chunk, ref devId);
    JsonObjectUti.GetTarget("floatNo", i_chunk, ref floatNo);

    // End of Extraction
    string chunk = "";
    // Create a template using the workbench
    using (JsonWriter workbench = new JsonTextWriter())
    {
        workbench.WriteStartObject();
        workbench.WriteMember("devId");
        workbench.WriteString(devId);
        workbench.WriteMember("buffer");
        workbench.WriteStartArray();
        workbench.WriteNumber(floatNo);
                
        for (uint i = 1; i < 1024; i++)
        {
            workbench.WriteNumber(0.0F);
        }
        workbench.WriteEndArray();

        // Additional field:
        InitializeTimeStamps(workbench, i_chunk);

        // Additional field:
        InitializeSeqNo(workbench, i_chunk);

        workbench.WriteEndObject();
        chunk = workbench.ToString();
    }
    return chunk;
}

static string UpdateFpBlob(string dwnld_chunk, string payload_chunk)
{
    // Extraction of payload_chunk
    string floatNo = "";
    string devId = "";
    JsonObjectUti.GetTarget("devId", payload_chunk, ref devId);
    JsonObjectUti.GetTarget("floatNo", payload_chunk, ref floatNo);

    // End of Extraction

    string tmp = "";
    string outputChunk = "";
    JsonObjectUti.GetTarget("buffer", dwnld_chunk, ref tmp);

    List<string> fpList = new List<string>();
    JsonListUti.SplitList(tmp, ref fpList);
    fpList.Insert(0, floatNo);
    fpList.RemoveAt(fpList.Count - 1);

    using (JsonWriter workbench = new JsonTextWriter())
    {
        workbench.WriteStartObject();
        workbench.WriteMember("devId");
        workbench.WriteString(devId);
        workbench.WriteMember("buffer");
        workbench.WriteStartArray();

        for (int i = 0; i < fpList.Count; i++)
        {
            workbench.WriteNumber(fpList[i]);
        }

        workbench.WriteEndArray();

        // the downloaded chunk might be of the old school type, haha ^ - ^
        // The following doesn't consider the field timeStamps:
        string timeStampsChunk = "";

        if (!JsonObjectUti.GetTarget("timeStamps", dwnld_chunk, ref timeStampsChunk))
        {
            // Initialize with the spec of the payload_chunk
            // see if timeStamp is specified in the payload
            InitializeTimeStamps(workbench, payload_chunk);
        }
        else
        {
            UpdateTimeStamps(workbench, dwnld_chunk, payload_chunk);
        }

        string seqNoChunk = "";
        if (!JsonObjectUti.GetTarget("seqNumbers", dwnld_chunk, ref seqNoChunk))
        {
            // Initialize with the spec of the paylod_chunk
            // See if seqNo is specified in the payload
            InitializeSeqNo(workbench, payload_chunk);
        }
        else
        {
            UpdateSeqNo(workbench, dwnld_chunk, payload_chunk);
        }

        workbench.WriteEndObject();
        outputChunk = workbench.ToString();
    }
    return outputChunk;
}

static void InitializeTimeStamps(JsonWriter workbench, string payloadChunk)
{
    string timeStampChunk = "";
    if (!JsonObjectUti.GetTarget("timeStamp", payloadChunk, ref timeStampChunk))
    {
        // Okay, timeStamp is not specified within the payload
        timeStampChunk = "";
    }

    workbench.WriteMember("timeStamps");                    //     "timeStamps":
    workbench.WriteStartArray();                            //     [

    workbench.WriteStartArray();                            //         [
    
    try
    {
        // timeStampChunk provided, and we need to make sure that the format is legit.
        DateTime timeStamp = DateTime.Parse(timeStampChunk, null, System.Globalization.DateTimeStyles.RoundtripKind);
        workbench.WriteString(timeStamp.ToString("o"));                       //             "timeSent_0",
    }
    catch (FormatException e)
    {
        // Well, the timeStamp provided is not legit
        workbench.WriteString("");
    }
            
    workbench.WriteString(DateTime.UtcNow.ToString("o"));   //             "timeRcv_0"
    workbench.WriteEndArray();                              //         ],
    for (uint i = 1; i < 1024; i++)
    {                                                       //         ...
        workbench.WriteStartArray();                        //         [
        workbench.WriteString("");                          //             "",
        workbench.WriteString("");                          //             ""
        workbench.WriteEndArray();                          //         ]
    }
    workbench.WriteEndArray();
}

static void UpdateTimeStamps(JsonWriter workbench, string dwnld_chunk, string payload_chunk)
{
    string timeStampsChunk = "";
    JsonObjectUti.GetTarget("timeStamps", dwnld_chunk, ref timeStampsChunk);
    List<string> timeStamps = new List<string>();
    JsonListUti.SplitList(timeStampsChunk, ref timeStamps);

    string timeStampChunk = "";
    if (JsonObjectUti.GetTarget("timeStamp", payload_chunk, ref timeStampChunk))
    {
        // field exists
        // Check if the format is legit
        try
        {
            DateTime timeStamp = DateTime.Parse(timeStampChunk, null, System.Globalization.DateTimeStyles.RoundtripKind);
            // timeStampChunk passes the datetime check, no modification required
        }
        catch (FormatException e)
        {
            timeStampChunk = "";
            // Format wrong + exception caught
            // The chunk is set to empty (as if no field is provided)
        }
    }
    else
    {
        // No field provided, the chunk is set to empty
        timeStampChunk = "";
    }

    // Start to write our 2D matrix
    workbench.WriteMember("timeStamps");
    workbench.WriteStartArray();

    // First element of our updated array needs to be taken care
    workbench.WriteStartArray();
    workbench.WriteString(timeStampChunk);
    workbench.WriteString(DateTime.UtcNow.ToString("o"));
    workbench.WriteEndArray();

    // It's time to iterate through the timeStamps and write the rest of the matrix
    // but first, we need to pop back the old list
    timeStamps.RemoveAt(timeStamps.Count - 1);

    foreach (var elem in timeStamps)
    {
        List<string> timeStampPair = new List<string>();
        // Each of the elements is a chunk by itself
        // There is a chunk in each element
        JsonListUti.SplitList(elem, ref timeStampPair);

        workbench.WriteStartArray();
        foreach (var item in timeStampPair)
        {
            workbench.WriteString(item);
        }
        workbench.WriteEndArray();
    }
    workbench.WriteEndArray();
}

static void UpdateSeqNo(JsonWriter workbench, string dwnld_chunk, string payload_chunk)
{
    // Extract seqNo from payload
    string seqNo = "2000";
    // First, check if the field exists?
    if (JsonObjectUti.GetTarget("seqNo", payload_chunk, ref seqNo))
    {
        // Check if the seqNo given is legit
        if (Int32.TryParse(seqNo, out int sequence_given))
        {
            if (!(sequence_given >= 0 && sequence_given < 1024))
            {
                seqNo = "2000";
            }
            // workbench.WriteNumber(seqNo);                //       <sequence-number>,
        }
        else
        {
            seqNo = "2000";                                 //       2000,
        }                                                   //       2000,
    }                                                       //       ...
    else
    {
        seqNo = "2000";
    }

    List<string> seqNumbers = new List<string>();
    string seqNumbersChunk = "";

    JsonObjectUti.GetTarget("seqNumbers", dwnld_chunk, ref seqNumbersChunk);
    JsonListUti.SplitList(seqNumbersChunk, ref seqNumbers);
    seqNumbers.Insert(0, seqNo);
    seqNumbers.RemoveAt(seqNumbers.Count - 1);

    workbench.WriteMember("seqNumbers");
    workbench.WriteStartArray();
    foreach (var elem in seqNumbers)
    {
        workbench.WriteNumber(elem);
    }
    workbench.WriteEndArray();
}

static void InitializeSeqNo(JsonWriter workbench, string i_chunk)
{
    string seqNo = "2000";

    // First, check if the field exists?
    if (JsonObjectUti.GetTarget("seqNo", i_chunk, ref seqNo))
    {
        // Check if the seqNo given is legit
        if (Int32.TryParse(seqNo, out int sequence_given))
        {
            if (!(sequence_given >= 0 && sequence_given < 1024))
            {
                seqNo = "2000";
            }
            // workbench.WriteNumber(seqNo);                //       <sequence-number>,
        }
        else
        {
            seqNo = "2000";                                 //       2000,
        }                                                   //       2000,
    }                                                       //       ...
    else
    {
        seqNo = "2000";
    }

    workbench.WriteMember("seqNumbers");                    //    "seqNumbers":
    workbench.WriteStartArray();                            //    [
    workbench.WriteNumber(seqNo);
    for (uint i = 1; i < 1024; i++)
    {
        workbench.WriteNumber("2000");                      //       2000
    }
    workbench.WriteEndArray();                              //    ]
}
