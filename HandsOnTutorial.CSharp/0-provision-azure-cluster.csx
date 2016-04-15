/*** hide ***/
#load "AzureCluster.csx"

using MBrace.Azure;
using MBrace.Azure.Management;

// IMPORTANT:
// Before running this tutorial, make sure that you install binding redirects to FSharp.Core
// on your C# Interactive executable. To do this, find "InteractiveHost.exe" in your Visual Studio 2015 Installation.
// From your command prompt, enter the following command:
//
//   explorer.exe /select, %programfiles(x86)%\Microsoft Visual Studio 14.0\Common7\IDE\PrivateAssemblies\InteractiveHost.exe
//
// Find the file "config/InteractiveHost.exe.config" that is bundled in the C# tutorial folder and place it next to InteractiveHost.exe.
// Now, reset your C# Interactive session. You are now ready to start using your C# REPL.

/**

# Provisioning your cluster using C# interactive

If using a locally simulated cluster(via ThespianCluster.csx) you can ignore this tutorial.

In this tutorial you learn how you can use the[MBrace.Azure.Management](https://www.nuget.org/packages/MBrace.Azure) library
to provision an MBrace cluster using C# Interactive. In order to proceed, you will need
to[sign up](https://azure.microsoft.com/en-us/pricing/free-trial/) for an Azure subscription. Once signed up, 
[download your publication settings file](https://manage.windowsazure.com/publishsettings).

Before proceeding, please go to `AzureCluster.fsx` and set your azure authentication data and deployment preferences.
Once done, we can reload the script.

*/

#load "AzureCluster.csx"

/**

Now let's create a new cluster by calling

*/

var deployment = Config.ProvisionCluster();

/**

We can track the provisioning progress of the cluster by calling

*/

deployment.ShowInfo();

/**

Provisioning can take some time(approximately 5 minutes).
Once done, you can now connect to your cluster as follows:

*/

var cluster = Config.GetCluster();

cluster.ShowWorkers();

/**

You can now run any of the subsequent tutorials and examples in your Azure cluster.

## Modifying the cluster

You can resize the cluster by calling

*/

Config.ResizeCluster(newVmCount: 20);

/**

When done, it is important to make sure that the cluster has been deprovisioned

*/

Config.DeleteCluster();

/**

## Summary

In this tutorial, you've learned how to provision an MBrace clusters running on Azure using MBrace.Azure.Management.
You are now ready to proceed with further samples to learn more about the MBrace programming model.

*/