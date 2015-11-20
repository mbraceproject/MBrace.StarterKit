(*** hide ***)
#load "AzureCluster.fsx"

open MBrace.Azure
open MBrace.Azure.Management

(**

# Provisioning your cluster using F# interactive

> If using a locally simulated cluster (via ThespianCluster.fsx) you can ignore this tutorial.

> This tutorial is from the [MBrace Starter Kit](https://github.com/mbraceproject/MBrace.StarterKit).

In this tutorial you learn how you can use the [MBrace.Azure.Management](https://www.nuget.org/packages/MBrace.Azure) library
to provision an MBrace cluster using F# Interactive. In order to proceed, you will need
to [sign up](https://azure.microsoft.com/en-us/pricing/free-trial/) for an Azure subscription. Once signed up, 
[download your publication settings file ](https://manage.windowsazure.com/publishsettings).

*)

// replace with your local .publishsettings path
let pubSettingsFile = @"C:\Users\...\Downloads\..." 

(**

Next, specify your region and the size and number of your virtual machines, and create your cluster:

*)
    
let region = Region.North_Europe
let vmSize = VMSize.Large
let vmCount = 4
    
let deployment = Deployment.Provision(pubSettingsFile, region, vmSize=vmSize, vmCount=vmCount) 

(**
This will provision a 4-worker MBrace cluster of Large (A3) instances
Provisioning can take some time (approximately 5 minutes). 
You can now connect to your cluster as follows:

*)

let cluster = AzureCluster.Connect(deployment.Configuration, logger = ConsoleLogger(true), logLevel = LogLevel.Info)

(**
You now have a connection to your cluster.

However you should now also record your cluster details in ``AzureCluster.fsx`` by evaluating the following code snippet:

*)

Config.RecordClusterDetails (pubSettingsFile, deployment.ServiceName)

(**

This lets you reconnect to your cluster later by using the following:

    #load "AzureCluster.fsx"
    let cluster = Config.GetCluster()

## Summary

In this tutorial, you've learned how to provision an MBrace clusters running on Azure using MBrace.Azure.Management.
You are now ready to proceed with further samples to learn more about the MBrace programming model.  

To learn more about provisioning, monitoring, scaling and deleting clusters using MBrace.Azure.Management
see [going-further/200-managing-azure-clusters.fsx](going-further/200-managing-azure-clusters.html).

*)


