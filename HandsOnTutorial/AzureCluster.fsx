#I "../packages/MBrace.Azure/tools" 
#I "../packages/Streams/lib/net45" 
#r "../packages/Streams/lib/net45/Streams.Core.dll"
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
    // The serice bus connection string is of the form
    //    Endpoint=sb://%s.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=%s

    let myStorageConnectionString = "DefaultEndpointsProtocol=https;AccountName=msrcyiwe;AccountKey=IfmbnhCPrIdWMUzfw5EzFHH7KQLdgP1tIA6NuDaqEVTP+QQ8HI1YCzfO9aAEJt+n79Dn1k07XKQL9kLu2LP6cQ=="
    let myServiceBusConnectionString = "Endpoint=sb://msrcmbrace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=1XeMNubcpNzq+CjK27PTlJ6fNDlpndFh+aWmYlNX/p0="

    // Alternatively you can specify the connection strings by calling the functions below
    //
    // storageName: the one you specified when you created cluster.
    // storageAccessKey: found under "Manage Access Keys" for that storage account in the Azure portal.
    // serviceBusName: the one you specified when you created cluster.
    // serviceBusKey: found under "Configure" for the service bus in the Azure portal
    
    // let createStorageConnectionString(storageName, storageAccessKey) = sprintf "DefaultEndpointsProtocol=https;AccountName=%s;AccountKey=%s" storageName storageAccessKey
    // let createServiceBusConnectionString(serviceBusName, serviceBusKey) = sprintf "Endpoint=sb://%s.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=%s" serviceBusName serviceBusKey

    let config = Configuration(myStorageConnectionString, myServiceBusConnectionString)

    let GetCluster() =
        let cluster = AzureCluster.Connect(config, logger = ConsoleLogger(true), logLevel = LogLevel.Info)
        cluster