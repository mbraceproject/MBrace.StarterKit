# MBrace on Azure


See [Getting started with MBrace on Azure](http://mbrace.io/#try-azure).

###  Manually Creating a Custom Package to Deploy

An MBrace cluster is provisioned on Azure by deploying an Azure Cloud Service with MBrace Worker Roles.
The basic provisioning process creates storage, service bus and virtual machine assets in Azure using
[default packages created as part of MBrace.Azure releases](https://github.com/mbraceproject/MBrace.Azure/releases).

You can build your own artisan packages that contain pre-installed software, adjusted settings or installation scripts.
The directory contains template solutions for this.

By building an artisan cloud service you can:

* adjust the endpoints to your cloud service from the defaults (so your MBrace cluster can publish 
  TCP and HTTP endpoints, either public or to your virtual network, 
  for example, you want your MBrace cluster to publish a web server); or

* enable Remote Access to MBrace cluster worker instances; or

* specify the size of local storage available on MBrace cluster worker instances; or

* upload certificates as part of your provisioning process; or

* specify Azure-specific local caching options; or

* include additional web and worker roles in your cloud service; or

* compile and deploy your own version of the MBrace cluster worker instance software. 

In order to provision explicitly, as a prerequisite you need 
to have an Azure account and basic knowledge of Azure computing.

You will need the [Azure SDK 2.7](http://azure.microsoft.com/en-us/downloads/).

## Provisioning your custom MBrace service

1. Make any appropriate changes to your Worker role implementation.
2. Set the desired VM size by double clicking the `Roles/MBrace.Azure.WorkerRole` item in the cloud service project.
3. Right click the `MBrace.Azure.CloudService` project and hit `Package..`. This will create a new cspkg in your local bin folder.
4. Open `HandsOnTutorial.sln` in the StarterKit and find [`AzureCluster.fsx`](https://github.com/mbraceproject/MBrace.StarterKit/blob/master/HandsOnTutorial/AzureCluster.fsx). Follow the instructions and be sure to set the `useCustomCloudService` binding to `true`. Save any changes to `AzureCluster.fsx`.
5. Open [0-provision-azure-cluster.fsx](https://github.com/mbraceproject/MBrace.StarterKit/blob/master/HandsOnTutorial/0-provision-azure-cluster.fsx). Follow the instructions and provision your custom cloud service.
6. Run any of the supplied samples and tutorials.
   with the script-based deployment process above:

## Provisioning using Visual Studio

Alternatively, you can provision and deploy from Visual Studio. Right click and Publish the `MBrace.Azure.CloudService` project. During publication, choose a new name for your cloud service. After your service is published it should appear as a cloud service in the [Azure management portal](https://manage.windowsazure.com/).

If using the hands-on tutorials, insert the same connection trings in [MBraceCluster.fsx](../HandsOnTutorial/AzureCluster.fsx#L24) and connect 
to your runtime (see [hello-world.fsx](../HandsOnTutorial/1-hello-world.fsx) for an example).
