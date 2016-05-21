# MBrace.AWS.WebWorker

Provides an IIS hosted MBrace.AWS worker implementation that can be hosted by AWS's ElasticBeanstalk.

### Installing the AWS SDK

Before proceeding please install the [AWS toolkit for Visual Studio](https://aws.amazon.com/visualstudio/).

### Setting up the Web Worker

Open up 
[`Config.cs`](https://github.com/mbraceproject/MBrace.StarterKit/blob/master/aws/MBrace.AWS.WebWorker/Config.cs) 
and fill out the following fields:

* `Region`: The prefered AWS data center for deploying your cluster.
* `AccessKey` and `SecretKey`: Credentials key pair corresponding to your user.
User must have full access to S3, DynamoDB and SQS. 

The above information uniquely identifies yours cluster,
so make sure that all workers/clients are providing the same configuration.

### Testing the web worker

You can test your configuration by building and debugging your web worker in your local machine.
Once the worker is up, use your AWS client instance to verify that the worker is attached to the cluster state.

### Deploying to Elastic Beanstalk

Right click on the web worker project and select "Publish to AWS". Follow the on-screen instructions
to configure your new cluster. Please ensure that the region matches your `Config.cs` setting.

### Connecting to your cluster

An MBrace.AWS cluster is uniquely identified by its region and credentials key pair. You can connect to
your deployed cluster by specifying that information on the client side:
```csharp
using MBrace.AWS;

var credentials = new MBraceAWSCredentials("AccessKey", "SecretKey");
var configuration = new Configuration(AWSRegion.EUCentral1, credentials);
var cluster = AWSCluster.Connect(configuration);
```