#I __SOURCE_DIRECTORY__
#I "../packages/MBrace.Azure/tools" 
#I "../packages/Streams/lib/net45" 
#r "../packages/Streams/lib/net45/Streams.dll"
#I "../packages/MBrace.Flow/lib/net45" 
#r "../packages/MBrace.Flow/lib/net45/MBrace.Flow.dll"
#load "../packages/MBrace.Azure/MBrace.Azure.fsx"
#load "../packages/MBrace.Azure.Management/MBrace.Azure.Management.fsx"

namespace global

module Config =

    open System.IO
    open MBrace.Core
    open MBrace.Runtime
    open MBrace.Azure
    open MBrace.Azure.Management

    type Deployment with 
        static member GetDeployment(pubSettingsFile,clusterName) = 
            let mgr = SubscriptionManager.FromPublishSettingsFile(pubSettingsFile, Region.North_Europe)
            mgr.GetDeployment(clusterName)

    // This script is used to reconnect to your cluster.

    // You can download your publication settings file at 
    //     https://manage.windowsazure.com/publishsettings
    let pubSettingsFile = @"C:\Users\dsyme\Downloads\MSRC-UK CC70550-10-29-2015-credentials.publishsettings" 

    // Your cluster name is reported when you create your cluster, or can be found in 
    // the Azure management console.
    let clusterName = "mbracefce23447" 

    /// Get the deployment for the cluster
    let GetDeployment() = Deployment.GetDeployment(pubSettingsFile, clusterName) 

    /// Connect to the cluster 
    let GetCluster() = 
        let deployment = GetDeployment()
        AzureCluster.Connect(deployment.Configuration, logger = ConsoleLogger(true), logLevel = LogLevel.Info)

    /// Modify this file to record the cluster details
    let RecordClusterDetails(pubSettingsFile, clusterName) = 

        let file = Path.Combine(__SOURCE_DIRECTORY__, __SOURCE_FILE__)
        let lines = 
            [ for line in File.ReadAllLines(file) ->
                 if line.Trim().StartsWith("let pubSettingsFile") then 
                     sprintf """    let pubSettingsFile = @"%s" """ pubSettingsFile
                 elif line.Trim().StartsWith("let clusterName") then 
                     sprintf """    let clusterName = "%s" """ clusterName
                 else line ]
        File.WriteAllLines(file,lines)
