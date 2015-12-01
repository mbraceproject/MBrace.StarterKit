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

    // This script is used to reconnect to your cluster.

    // You can download your publication settings file at 
    //     https://manage.windowsazure.com/publishsettings
    let pubSettingsFile = @"C:\path\to\your.publishsettings"

    // If your publication settings defines more than one subscription,
    // you will need to specify which one you will be using here.
    let subscriptionId : string option = None

    // Your prefered Azure service name for the cluster.
    // NB: must be a valid DNS prefix unique across Azure.
    let clusterName = "replace with a valid and unique cloud service name"

    // Your prefered Azure region. Assign this to a data center close to your location.
    let region = Region.North_Europe
    // Your prefered VM size
    let vmSize = VMSize.Large
    // Your prefered cluster count
    let vmCount = 4

    // set to true if you would like to provision
    // the custom cloud service bundled with the StarterKit
    // In order to use this feature, you will need to open
    // the `CustomCloudService` solution under the `azure` folder 
    // inside the MBrace.StarterKit repo.
    // Right click on the cloud service item and hit "Package.."
    let useCustomCloudService = false
    let private tryGetCustomCsPkg () =
        if useCustomCloudService then
            let path = __SOURCE_DIRECTORY__ + "/../azure/CustomCloudService/bin/app.publish/MBrace.Azure.CloudService.cspkg" |> Path.GetFullPath
            if not <| File.Exists path then failwith "Find the 'MBrace.Azure.CloudService' project under 'azure\CustomCloudService' and hit 'Package...'."
            Some path
        else
            None

    /// Gets the already existing deployment
    let GetDeployment() = Deployment.GetDeployment(pubSettingsFile, serviceName = clusterName, ?subscriptionId = subscriptionId) 

    /// Provisions a new cluster to Azure with supplied parameters
    let ProvisionCluster() = 
        Deployment.Provision(pubSettingsFile, region, vmCount, vmSize, serviceName = clusterName, ?subscriptionId = subscriptionId, ?cloudServicePackage = tryGetCustomCsPkg())

    /// Resizes the cluster using an updated VM count
    let ResizeCluster(newVmCount : int) =
        let deployment = GetDeployment()
        deployment.Resize(newVmCount)

    /// Deletes an existing cluster deployment
    let DeleteCluster() =
        let deployment = GetDeployment()
        deployment.Delete()

    /// Connect to the cluster 
    let GetCluster() = 
        let deployment = GetDeployment()
        AzureCluster.Connect(deployment, logger = ConsoleLogger(true), logLevel = LogLevel.Info)