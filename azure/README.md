# MBrace on Azure

## Provisioning Your Cluster

An MBrace cluster can be provisioned on Azure using the following methods:

### Use Brisk by Elastacloud

Go to http://www.briskengine.com/ and follow the on-screen instructions.
A detailed tutorial can be found [here](https://github.com/mbraceproject/MBrace.StarterKits/blob/master/azure/brisk-tutorial.md).

### Creating a Custom Azure Cloud Service with MBrace Worker Roles

The directory contains a collection of solutions for provisioning custom MBrace cloud services on Azure.

1. Clone this repo.
2. You will need the [Azure SDK](http://azure.microsoft.com/en-us/downloads/) for the WorkerRole projects.
3. Depending on your Visual Studio/F# installation, select an appropriate solution from the azure folder.
4. Insert your Service Bus and Azure Storage connection strings in the `ServiceConfiguration.Cloud.cscfg` file.
5. Set the desired number of worker nodes by updating the instance count attribute in the `ServiceConfiguration.Cloud.cscfg` file.
6. Set the desired [instance size](https://azure.microsoft.com/en-us/documentation/articles/virtual-machines-size-specs/) by updating the vmsize attribute in the `ServiceDefinition.csdef` file.
7. Build your solution.
8. Right click and Publish the MBraceAzureService project.
9. If using the hands-on tutorials, insert the same connection 
   strings in [MBraceCluster.fsx](../HandsOnTutorial/AzureCluster.fsx#L24) and connect 
   to your runtime (see [hello-world.fsx](../HandsOnTutorial/1-hello-world.fsx) for an example).