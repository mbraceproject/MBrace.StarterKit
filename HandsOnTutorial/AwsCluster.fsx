#I __SOURCE_DIRECTORY__
#I "../packages/MBrace.AWS/tools" 
#I "../packages/Streams/lib/net45" 
#r "../packages/Streams/lib/net45/Streams.dll"
#I "../packages/MBrace.Flow/lib/net45" 
#r "../packages/MBrace.Flow/lib/net45/MBrace.Flow.dll"
#load "../packages/MBrace.AWS/MBrace.AWS.fsx"

#nowarn "445"

namespace global

module Config =

    open System.IO
    open MBrace.Core
    open MBrace.Runtime
    open MBrace.AWS

    /// AWS Credential source identifier for the client session
    type CredentialSource =
        | CredentialStore of profileName:string
        | ProvidedKey of accessKey:string * secretKey:string
        | FromEnvironmentVariables // $AWS_ACCESS_KEY_ID and $AWS_SECRET_ACCESS_KEY

    // Fill out 

    // 1. your prefered AWS region. Assign this to a data center close to your location
    let region : AWSRegion = AWSRegion.EUCentral1

    // 2. Your prefered credential source. Either specify a set of keys present in your local
    //    SDK credential store or provide a pair of keys.
    let credentialSource : CredentialSource = 
        CredentialStore("default")
//        ProvidedKey("myAccessKey", "mySecretKey")

    // 3. Your prefered resource prefix. Identifies a string prefix put before any AWS resource
    //    that this cluster initializes (S3, DynamoDB and SQS entities).
    let resourcePrefix : string option = None


    /// Gets the MBrace.AWS.Configuration object used to identify our MBrace cluster
    let GetConfiguration() = 
        let credentials =
            match credentialSource with
            | FromEnvironmentVariables -> MBraceAWSCredentials.FromEnvironmentVariables()
            | CredentialStore pf -> MBraceAWSCredentials.FromCredentialsStore(pf)
            | ProvidedKey(ak,sk) -> new MBraceAWSCredentials(ak, sk)

        Configuration.Define(region, credentials, ?resourcePrefix = resourcePrefix)

    /// Initializes an MBrace.AWS client instance which connects to cluster with given configuration
    let GetCluster() = AWSCluster.Connect(GetConfiguration(), logger = ConsoleLogger(), logLevel = LogLevel.Info)

    /// Attaches specified number of local workers to the current cluster state
    let AttachLocalWorkers (cluster : AWSCluster) (numWorkers : int) : unit =
        cluster.AttachLocalWorkers(numWorkers)

    /// Deletes *all* AWS resources associated with the current cluster configuration
    let DeleteCluster() = 
        GetCluster().Reset(deleteQueues = true, deleteRuntimeState = true, deleteLogs = true, deleteUserData = true, force = true, reactivate = false)


    /// Persist supplied access key and secret to local AWS SDK credentials store with given profile name
    /// (Windows only)
    let SaveToCredentialStore (profileName : string) (accessKey : string, secretKey : string) =
        Amazon.Util.ProfileManager.RegisterProfile(profileName, accessKey, secretKey)