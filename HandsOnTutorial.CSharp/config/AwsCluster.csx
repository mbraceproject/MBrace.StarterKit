#r "../../packages/FSharp.Core/lib/net40/FSharp.Core.dll"
#r "../../packages/System.Runtime.Loader/lib/netstandard1.5/System.Runtime.Loader.dll"
#r "../../packages/MBrace.AWS/tools/MBrace.Core.dll"
#r "../../packages/MBrace.AWS/tools/MBrace.Runtime.dll"
#r "../../packages/MBrace.AWS/tools/MBrace.AWS.dll"
#r "../../packages/Streams/lib/net45/Streams.dll"
#r "../../packages/MBrace.Flow/lib/net45/MBrace.Flow.dll"
#r "../../packages/MBrace.CSharp/lib/net45/MBrace.CSharp.dll"

using System;
using System.IO;
using MBrace.Runtime;
using MBrace.Core.CSharp;
using MBrace.AWS;

public class Config
{
    static Config()
    {
        // Fill out configuration settings here

        // 1. Your prefered AWS region. Assign this to a data center close to your location.
        Region = AWSRegion.EUCentral1;

        // 2. Your prefered AWS credentials to be used by the cluster.
        //    You can either specify a profile name for the local credential store
        ProfileName = "default";
        //    Or inline your credentials in the following lines
        AccessKey = null;
        SecretKey = null;

        // 3. (optional) specify a resource prefix for all AWS resources created by the cluster
        //    (buckets, tables, queues, etc.)
        ResourcePrefix = null;

        AWSWorker.LocalExecutable = "../packages/MBrace.AWS/tools/mbrace.awsworker.exe";
    }

    /// <summary>
    ///   Gets or sets the default profile name for local AWS SDK credentials store
    /// </summary>
    public static string ProfileName { get; set; }

    /// <summary>
    ///   Gets or sets the AWS access key to be used by the cluster
    /// </summary>
    public static string AccessKey { get; set; }

    /// <summary>
    ///   Gets or sets the AWS secret key to be used by the cluster
    /// </summary>
    public static string SecretKey { get; set; }

    /// <summary>
    ///   Gets or sets a common prefix to be placed in every AWS resource allocated by the current cluster
    /// </summary>
    public static string ResourcePrefix { get; set; }

    /// <summary>
    ///     Gets or sets the default region to deploy your cluster to
    /// </summary>
    public static AWSRegion Region { get; set; }


    public static Configuration GetConfiguration()
    {
        Amazon.Runtime.AWSCredentials credentials;

        if (AccessKey != null && SecretKey != null) credentials = new MBraceAWSCredentials(AccessKey, SecretKey);
        else if (ProfileName != null) credentials = Amazon.Util.ProfileManager.GetAWSCredentials(ProfileName);
        else throw new ArgumentException("No ProfileName or AccessKey has been specified by the user!");

        var prefix = (ResourcePrefix != null) ? ResourcePrefix.ToOption() : Option.None<string>();
        return Configuration.Define(Region, credentials, prefix);
    }

    /// <summary>
    ///     Deletes a cluster with supplied parameters, if it exists.
    /// </summary>
    public static void DeleteCluster()
    {
        var cluster = GetCluster();
        var to = true.ToOption();
        var fo = false.ToOption();
        cluster.Reset(deleteQueues: to, deleteRuntimeState: to, deleteLogs: to, 
                        deleteUserData: to, force: to, reactivate: fo);
    }

    /// <summary>
    /// Gets an AzureCluster instance with supplied parameters for use with computations
    /// </summary>
    /// <returns></returns>
    public static AWSCluster GetCluster()
    {
        var config = GetConfiguration();
        var logger = (MBrace.Runtime.ISystemLogger)new MBrace.Runtime.ConsoleLogger();
        return AWSCluster.Connect(config, logger: logger.ToOption(), logLevel: LogLevel.Info.ToOption());
    }
}