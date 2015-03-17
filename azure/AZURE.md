## MBrace on Azure

An MBrace cluster can be provisioned on Azure using the following methods

## Use Brisk by Elastacloud

Go to http://www.briskengine.com/ and follow the on-screen instructions.
A detailed tutorial can be found [here](https://github.com/mbraceproject/MBrace.StarterKits/blob/master/azure/brisk-tutorial.md).

## Creating a Custom Azure Cloud Service with MBrace Worker Roles

The directory [CustomCloudService](CustomCloudService) contains a sample showing how to create a custom cloud service
for Azure where the worker roles include MBrace worker roles.

1. Clone this repo.
2. You will need the [Azure SDK](http://azure.microsoft.com/en-us/downloads/) for the WorkerRole project.
3. Insert your Service Bus and Azure Storage connection strings in the [service configuration](CustomCloudService/MBraceAzureService/ServiceConfiguration.Cloud.cscfg).
4. Build the three projects.
5. Publish the MBraceAzureService project.
6. Insert the same connection strings in [client.fsx](MBraceAzureClient/client.fsx#L15) and connect to your runtime.

