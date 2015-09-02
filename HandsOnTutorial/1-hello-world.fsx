(*** hide ***)
#load "Thespian.fsx"
#load "Azure.fsx"

open System
open System.IO
open MBrace.Core
open MBrace.Flow

// Initialize client object to an MBrace cluster:
let cluster = 
//    getAzureClient() // comment out to use an MBrace.Azure cluster; don't forget to set the proper connection strings in Azure.fsx
    initThespianCluster(4) // use a local cluster based on MBrace.Thespian; configuration can be adjusted using Thespian.fsx

(**
# Your First 'Hello World' Computation with MBrace

First you send a simple computation to an mbrace cluster using F# Interactive scripting.
You can also send computations from a compiled F# project, though using scripting is very 
common with MBrace.

A guide to creating a cluster is [here](http://www.m-brace.net/#try).

> NOTE: Before running, build this solution to get the required nuget packages, and edit Azure.fsx to enter your Azure connection strings.

First connect to the cluster using a configuration to bind to your storage and service bus on Azure.
*)    

// You can connect to the cluster and get details of the workers in the pool:
cluster.ShowWorkerInfo()

(** Now execute your first cloud workflow and get a handle to the running job: *)
let task = 
    cloud { return "Hello world!" } 
    |> cluster.CreateCloudTask

// You can get details for the task.
task.ShowInfo()

// Block until the result is computed by the cluster
let text = task.Result

(** Alternatively we can do this all in one line: *)
let quickText = 
    cloud { return "Hello world!" } 
    |> cluster.RunOnCloud

// You can view the history of processes:
cluster.ShowCloudTaskInfo()

(** To check that you are running in the cloud, compre
a workflow by running locally (using async semantics) with one
using remote execution: *)
let localResult =
    cloud { printfn "hello, world" ; return Environment.MachineName }
    |> cluster.RunOnCurrentProcess

let remoteResult =
    cloud { printfn "hello, world" ; return Environment.MachineName }
    |> cluster.RunOnCloud

(** 

## Controlling the Cluster

In case you run into trouble, this can be used to clear all process 
records in the cluster: 
*)

cluster.ClearAllCloudTasks()