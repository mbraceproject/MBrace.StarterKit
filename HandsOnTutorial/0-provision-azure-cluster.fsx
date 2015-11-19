(*** hide ***)
#load "AzureCluster.fsx"

open MBrace.Core
open MBrace.Azure
open MBrace.Azure.Management

(**

# Provisioning your MBrace.Azure cluster using F# interactive

> This tutorial is from the [MBrace Starter Kit](https://github.com/mbraceproject/MBrace.StarterKit).

In this tutorial you learn how you can use the [MBrace.Azure.Management](https://www.nuget.org/packages/MBrace.Azure) library
to provision, monitor and scale an MBrace cluster using Visual Studio and F# Interactive. In order to proceed, you will need
to [sign up](https://azure.microsoft.com/en-us/pricing/free-trial/) for an Azure subscription. Once signed up, follow 
the [instructions](https://msdn.microsoft.com/en-us/library/dn414650(v=nav.90).aspx) on how to download your private
`.publishsettings` file, which contains all authentication information necessary to manage your Azure subscription(s).

*)

let pubSettingsFile = @"C:\path\to\my.publishsettings" // replace with your local .publishsettings path

(**

Next, read your publish settings file by calling

*)

let pubSettings = PublishSettings.Parse pubSettingsFile

(** 

Yielding

    val pubSettings : PublishSettings =
      [|"Visual Studio Premium with MSDN"; "Azure in Open"; "Azure Free Trial"|]

Now we can select our subscription of preference

*)

let subscription = pubSettings.GetSubscriptionById "Azure Free Trial"

(**

Select your prefered Azure region. This specifies the default Azure data center to be used for your deployments.
Choosing a data center close to your location is recommended.

*)

let myRegion = Region.North_Europe

(**

Let's now instantiate our subscription manager instance

*)

let manager = SubscriptionManager.Create(subscription, myRegion)

(**

Provisioning an MBrace cluster is now straightforward. Simply write

*)

let deployment = manager.Provision(serviceName = "mbracetest", vmCount = 4, vmSize = VMSize.A3)

(**

Which should provision a 4-worker MBrace cluster of Large (A3) instances
Provisioning can take some time (approximately 5 minutes). We can track progress by running

*)

deployment.ShowInfo()

(**

Which yields

    Cloud Service "mbracetest"

    Name        Region        VM size  #Instances  Service Status  Deployment Status   Last Modified
    ----        ------        -------  ----------  --------------  -----------------   -------------
    mbracetest  North Europe  Large    4           Created         Provisioning 33.3%  19/11/2015 6:24:01 pm

Finally, when deployment has completed we can easily obtain a cluster instance by typing

*)

let cluster = AzureCluster.Connect deployment

(**

and submit our first computation to the cluster

*)

Cloud.CurrentWorker |> Cloud.ParallelEverywhere |> cluster.Run

(**

Resizing the cluster is as simple as calling

*)

deployment.Resize(vmCount = 20)

(**

When done, it's always a good idea to dispose of our deployment

*)

deployment.Delete()

(**

## Managing storage accounts

The subscription object can be used to manage Azure storage accounts

*)

let newAccount = manager.Storage.CreateAccount(accountName = "mynewaccount", region = Region.Southeast_Asia)

(**

or it can be used to retrieve already existing accounts

*)

let existingAccount = manager.Storage.GetAccount(accountName = "existingaccount")

(**

which can be used in subsequent cluster deployments

*)

let deployment' = manager.Provision(vmCount = 10, region = Region.Southeast_Asia, storageAccount = newAccount.AccountName)

(**

A list of all accounts can be viewed by writing

*)

manager.Storage.ShowAccounts()

(**

Yielding

    Azure Storage Accounts for subscription "Azure Free Trial"          

    Account Name    Region          Account Type  Status   Affinity Group    
    ------------    ------          ------------  ------   --------------    
    mynewaccount    Southeast Asia  Standard_LRS  Created  N/A
    existingaccount North Europe    Standard_LRS  Created  N/A

## Summary

In this tutorial, you've learned about MBrace.Azure.Management and how it can be used to deploy and manage MBrace clusters running on Azure.

*)
