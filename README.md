# ServerApp25
Extract float numbers from a predefined JSON format file (constantly coming out from Azure IoT Hub). Then update the record being kept in the Azure Storage.

Files are written to be hosted on Azure Functions.

One needs to subscribe for Azure Functions, create an Azure Function App, do some configurations before uploading the files.

A key-value pair of "StorageConnectionString":<connection sting of Azure Blob Storag subscribed> needs to be created and stored in the application settings in order to make this program works.
