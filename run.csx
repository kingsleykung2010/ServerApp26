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

        JsonObjectUti.GetTarget("devId", receivedChunk, ref deviceId);
        JsonObjectUti.GetTarget("floatNo", receivedChunk, ref floatNo);

        if (DEBUG_FUNCTION)
        {
            log.Info($"Message Detected.\n");
            log.Info($"Device Id: {deviceId}\n");
            log.Info($"Float No.: {floatNo}\n");
        }
        // Currently, we consider floating point only
        // Register device into the list
        InsertDevToList(deviceId, "fp-container");

        StoreFp(deviceId, floatNo);
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
        else
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
    else
    {
        i_chunk = "DisplayDevice(" + i_chunk + ")";
    }

    GetBlobRef(containerName, blobName).UploadText(i_chunk);
}

static void StoreFp(string devId, string floatNo)
{
    string blobName = devId + ".json";
    string containerName = "fp-container";
    string chunk = DownloadBlob(containerName, blobName);
    string toBeStored = "";

    if (chunk == "")
    {
        // Initialize the blob
        toBeStored = InitializeFpBlob(devId, floatNo);
    }
    else
    {
        // Update the existing blob
        toBeStored = UpdateFpBlob(chunk, devId, floatNo);
    }

    UploadBlob(toBeStored, containerName, blobName);
}

static string InitializeFpBlob(string devId, string floatNo)
{
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
        workbench.WriteEndObject();
        chunk = workbench.ToString();
    }
    return chunk;
}

static string UpdateFpBlob(string chunk, string devId, string floatNo)
{
    string tmp = "";
    string outputChunk = "";
    JsonObjectUti.GetTarget("buffer", chunk, ref tmp);

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
        workbench.WriteEndObject();
        outputChunk = workbench.ToString();
    }
    return outputChunk;
}
