# MBrace on Azure

## Hands On Tutorials

The directory [HandsOnTutorial](HandsOnTutorial) contains a set of scripted 
hands-on tutorials showing how to use an MBrace cluster from F# Interactive.

1. Provision your cluster, see below.
2. Open and build the solution ``MBrace.Azure.StarterKit.sln`` before working with the scripts
   to restore the necessary packages.

## Provisioning Your Cluster

An MBrace cluster can be provisioned on Azure using the following methods:

### Use Brisk by Elastacloud

Go to http://www.briskengine.com/ and follow the on-screen instructions.
A detailed tutorial can be found [here](https://github.com/mbraceproject/MBrace.StarterKits/blob/master/azure/brisk-tutorial.md).



### Creating a Custom Azure Cloud Service with MBrace Worker Roles

The directory [CustomCloudService](CustomCloudService) contains a sample showing how to create a custom cloud service
for Azure where the worker roles include MBrace worker roles.

1. Clone this repo.
2. You will need the [Azure SDK](http://azure.microsoft.com/en-us/downloads/) for the WorkerRole project.
3. Insert your Service Bus and Azure Storage connection strings in the [service configuration](CustomCloudService/MBraceAzureService/ServiceConfiguration.Cloud.cscfg).
4. Build the three projects.
5. Publish the MBraceAzureService project.
6. If using the hands-on tutorials, insert the same connection 
   strings in [credentials.fsx](HandsOnTutorial/credentials.fsx#L29) and connect 
   to your runtime (see [hello-world.fsx](HandsOnTutorial/1-hello-world.fsx) as an example).

