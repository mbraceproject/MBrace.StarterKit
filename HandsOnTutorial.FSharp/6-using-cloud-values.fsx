(*** hide ***)
#load "ThespianCluster.fsx"
//#load "AzureCluster.fsx"
//#load "AwsCluster.fsx"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit AzureCluster.fsx to enter your connection strings.

open System
open System.IO
open MBrace.Core
open MBrace.Flow

// Initialize client object to an MBrace cluster
let cluster = Config.GetCluster() 

(**
# Creating and Using Cloud Values 

> This tutorial is from the [MBrace Starter Kit](https://github.com/mbraceproject/MBrace.StarterKit).

You now learn how to upload data to cloud storage using CloudValue and
then process it using MBrace cloud tasks.

When using MBrace, data is implicitly uploaded if it is
part of the closure of a cloud workflow - for example, if a value is
referenced in a cloud { ... } block.  That data is a transient part of the 
process specification.  This is often the most convenient way to get 
small amounts (KB-MB) of data to the cloud: just use the data as part
of a cloud workflow and run that work in the cloud.

If you wish to persist data in the cloud - for example, if it is too big
to upload multiple times - then you can use one or more of the
cloud data constructs that MBrace provides. 
 
Note you can alternatively use any existing cloud storage 
APIs or SDKs you already have access to. 
 
If using Azure, you can copy larger data to Azure using the AzCopy.exe command line tool, see
[Transfer data with the AzCopy Command-Line Utility](https://azure.microsoft.com/en-us/documentation/articles/storage-use-azcopy/).
You can manage storage using the "azure" command line tool, see
[Install the Azure CLI](https://azure.microsoft.com/en-us/documentation/articles/xplat-cli/)
Also, if you wish you  can read/write using the .NET Azure storage SDKs directly rather than 
using MBrace primitives.

*)
 
(** Here's some data (~1.0MB) *)
let mkData () = String.replicate 10000 "The quick brown fox jumped over the lazy dog\r\n" 


(** Generate the data, upload to blob storage and return a handle to the stored data *)
let persistedCloudData = 
    cloud { 
            let data = mkData()
            let! cell = CloudValue.New data 
            return cell }
    |> cluster.Run

(** Run a cloud job which reads the blob and processes the data *)
let lengthOfData = 
    cloud { let readData = persistedCloudData.Value
            return readData.Length }
    |> cluster.Run

(** 
## Summary

In this tutorial, you've learned how to store values in cloud storage.
Continue with further samples to learn more about the MBrace programming model.  


> Note, you can use the above techniques from both scripts and compiled projects. To see the components referenced 
> by this script, see [ThespianCluster.fsx](ThespianCluster.html) or [AzureCluster.fsx](AzureCluster.html).

*)
