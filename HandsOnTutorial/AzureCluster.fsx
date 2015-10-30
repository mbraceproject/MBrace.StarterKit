#I __SOURCE_DIRECTORY__
#I "../packages/MBrace.Azure/tools" 
#I "../packages/Streams/lib/net45" 
#r "../packages/Streams/lib/net45/Streams.dll"
#I "../packages/MBrace.Flow/lib/net45" 
#r "../packages/MBrace.Flow/lib/net45/MBrace.Flow.dll"
#load "../packages/MBrace.Azure/MBrace.Azure.fsx"

namespace global

module Config =

    open MBrace.Core
    open MBrace.Runtime
    open MBrace.Azure

    // Both of the connection strings can be found under "Cloud Service" --> "Configure" --> scroll down to "MBraceWorkerRole"
    //
    // The storage connection string is of the form  
    //    DefaultEndpointsProtocol=https;AccountName=myAccount;AccountKey=myKey 
    //
    // The service bus connection string is of the form
    //    Endpoint=sb://%s.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=%s


    let myStorageConnectionString = "..."
    let myServiceBusConnectionString = "..."

    // Alternatively you can specify the connection strings by calling the functions below
    //
    // storageName: the one you specified when you created cluster.
    // storageAccessKey: found under "Manage Access Keys" for that storage account in the Azure portal.
    // serviceBusName: the one you specified when you created cluster.
    // serviceBusKey: found under "Configure" for the service bus in the Azure portal
    
    // let createStorageConnectionString(storageName, storageAccessKey) = sprintf "DefaultEndpointsProtocol=https;AccountName=%s;AccountKey=%s" storageName storageAccessKey
    // let createServiceBusConnectionString(serviceBusName, serviceBusKey) = sprintf "Endpoint=sb://%s.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=%s" serviceBusName serviceBusKey

    let config = Configuration(myStorageConnectionString, myServiceBusConnectionString)

    // It is possible to keep connection strings stored in the environment.
    // 1. To set the environment variables:
    // Configuration.EnvironmentStorageConnectionString <- "your storage connection string here"
    // Configuration.EnvironmentServiceBusConnectionString <- "your service bus connection string here"
    //
    // 2. To recover the environment variables
    // let config = Configuration.FromEnvironmentVariables()

    let GetCluster() =
        AzureCluster.Connect(config, logger = ConsoleLogger(true), logLevel = LogLevel.Info)

