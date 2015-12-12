#r "System.Net.dll"
#r "../../packages/FSharp.Core/lib/net40/FSharp.Core.dll"
#r "../../packages/System.Runtime.Loader/lib/DNXCore50/System.Runtime.Loader.dll"
#r "../../packages/MBrace.Azure/tools/FsPickler.dll"
#r "../../packages/MBrace.Azure/tools/Vagabond.dll"
#r "../../packages/MBrace.Azure/tools/Argu.dll"
#r "../../packages/MBrace.Azure/tools/Newtonsoft.Json.dll"
#r "../../packages/MBrace.Azure/tools/Microsoft.Data.Edm.dll"
#r "../../packages/MBrace.Azure/tools/Microsoft.Data.OData.dll"
#r "../../packages/MBrace.Azure/tools/Microsoft.WindowsAzure.Configuration.dll"
#r "../../packages/MBrace.Azure/tools/MBrace.Core.dll"
#r "../../packages/MBrace.Azure/tools/MBrace.Runtime.dll"
#r "../../packages/MBrace.Azure/tools/MBrace.Azure.dll"
#r "../../packages/Microsoft.WindowsAzure.Common/lib/net45/Microsoft.WindowsAzure.Common.dll"
#r "../../packages/Microsoft.WindowsAzure.Common/lib/net45/Microsoft.WindowsAzure.Common.NetFramework.dll"
#r "../../packages/Microsoft.Azure.Common/lib/net45/Microsoft.Azure.Common.dll"
#r "../../packages/Microsoft.Azure.Common/lib/net45/Microsoft.Azure.Common.NetFramework.dll"
#r "../../packages/Microsoft.Bcl.Async/lib/net40/Microsoft.Threading.Tasks.dll"
#r "../../packages/Hyak.Common/lib/net45/Hyak.Common.dll"
#r "../../packages/Microsoft.WindowsAzure.Management/lib/net40/Microsoft.WindowsAzure.Management.dll"
#r "../../packages/Microsoft.WindowsAzure.Management.Compute/lib/net40/Microsoft.WindowsAzure.Management.Compute.dll"
#r "../../packages/Microsoft.WindowsAzure.Management.Storage/lib/net40/Microsoft.WindowsAzure.Management.Storage.dll"
#r "../../packages/Microsoft.WindowsAzure.Management.ServiceBus/lib/net40/Microsoft.WindowsAzure.Management.ServiceBus.dll"
#r "../../packages/MBrace.Azure.Management/lib/net45/MBrace.Azure.Management.dll"
#r "../../packages/Streams/lib/net45/Streams.dll"
#r "../../packages/MBrace.Flow/lib/net45/MBrace.Flow.dll"
#r "../../packages/MBrace.CSharp/lib/net45/MBrace.CSharp.dll"

using MBrace.Core.CSharp;
using MBrace.Azure;
using MBrace.Azure.Management;

public class Config
{
    static Config()
    {
        // Fill out configuration settings here

        // You can download your publication settings file at 
        //     https://manage.windowsazure.com/publishsettings
        PublishSettings = "";

        // If your publication settings defines more than one subscription,
        // you will need to specify which one you will be using here.
        SubscriptionId = null;

        // Your prefered Azure service name for the cluster.
        // NB: must be a valid DNS prefix unique across Azure.
        ClusterName = "";

        // Your prefered Azure region. Assign this to a data center close to your location.
        Region = Region.North_Europe;
        // Your prefered VM size
        VMSize = VMSize.A3;
        // Your prefered cluster count
        WorkerCount = 4;

        // set to true if you would like to provision
        // the custom cloud service bundled with the StarterKit
        // In order to use this feature, you will need to open
        // the `CustomCloudService` solution under the `azure` folder 
        // inside the MBrace.StarterKit repo.
        // Right click on the cloud service item and hit "Package.."
        UseLocalCsPkg = false;

        AzureWorker.LocalExecutable = "../packages/MBrace.Azure/tools/mbrace.azureworker.exe";
    }

    /// <summary>
    ///     Gets or sets the path to the local Azure .publishsettings file
    /// </summary>
    public static string PublishSettings { get; set; }

    /// <summary>
    ///     Gets or sets the subscription identifier to be used from publishsettings
    /// </summary>
    public static string SubscriptionId { get; set; }

    /// <summary>
    ///     Gets or sets whether deployment should use a locally created custom cs package
    /// </summary>
    public static bool UseLocalCsPkg { get; set; }

    /// <summary>
    ///     Gets or sets the default region to deploy your cluster to
    /// </summary>
    public static Region Region { get; set; }

    /// <summary>
    ///     Gets or sets the cluster name to be used by the deployment
    /// </summary>
    public static string ClusterName { get; set; }

    /// <summary>
    ///     Gets or sets the desired number of Thespian worker instances
    /// </summary>
    public static int WorkerCount { get; set; }

    /// <summary>
    ///     Gets or sets the Azure VM size to be used by cluster deployments
    /// </summary>
    public static VMSize VMSize { get; set; }

    private static string GetLocalCsPkg()
    {
        var path = Path.GetFullPath("../azure/CustomCloudService/bin/app.publish/MBrace.Azure.CloudService.cspkg");
        if (!File.Exists(path))
            throw new InvalidOperationException(@"Find the 'MBrace.Azure.CloudService' project under 'azure\CustomCloudService' and hit 'Package...'.");
        return path;
    }

    /// <summary>
    ///     Gets an azure subscription manager using the specified configuration parameters
    /// </summary>
    /// <returns></returns>
    public static SubscriptionManager GetSubscriptionManager()
    {
        var logger = (MBrace.Runtime.ISystemLogger)new MBrace.Runtime.ConsoleLogger();
        var subscriptionId = (SubscriptionId != null) ? SubscriptionId.ToOption() : Option.None<string>() ;
        return SubscriptionManager.FromPublishSettingsFile(PublishSettings,
                                                            defaultRegion: Region,
                                                            subscriptionId: subscriptionId,
                                                            logger: logger.ToOption());
    }

    /// <summary>
    ///     Provisions a cluster using the supplied parameters
    /// </summary>
    /// <returns></returns>
    public static Deployment ProvisionCluster()
    {
        var manager = Config.GetSubscriptionManager();
        var localCsPkg = (UseLocalCsPkg) ? GetLocalCsPkg().ToOption() : Option.None<string>();
        return manager.Provision(vmCount: WorkerCount,
                                    serviceName: ClusterName.ToOption(),
                                    vmSize: VMSize.ToOption(),
                                    cloudServicePackage: localCsPkg);
    }

    /// <summary>
    ///     Gets a deployment with supplied parameters, if it exists.
    /// </summary>
    /// <returns></returns>
    public static Deployment GetDeployment()
    {
        var manager = Config.GetSubscriptionManager();
        return manager.GetDeployment(ClusterName);
    }

    /// <summary>
    ///     Deletes a cluster with supplied parameters, if it exists.
    /// </summary>
    public static void DeleteCluster()
    {
        var deployment = Config.GetDeployment();
        deployment.Delete();
    }

    /// <summary>
    /// Resizes azure cluster with supplied parameters, if it exists.
    /// </summary>
    /// <param name="newVmCount">New VM count to be used.</param>
    public static void ResizeCluster(int newVmCount)
    {
        var deployment = Config.GetDeployment();
        deployment.Resize(newVmCount);
    }

    /// <summary>
    /// Gets an AzureCluster instance with supplied parameters for use with computations
    /// </summary>
    /// <returns></returns>
    public static AzureCluster GetCluster()
    {
        var deployment = Config.GetDeployment();
        var logger = (MBrace.Runtime.ISystemLogger)new MBrace.Runtime.ConsoleLogger();
        return AzureCluster.Connect(deployment.Configuration, logger: logger.ToOption());
    }
}
