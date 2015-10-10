(*** hide ***)
#load "ThespianCluster.fsx"
//#load "AzureCluster.fsx"

open System
open System.IO
open MBrace.Core
open MBrace.Flow

// Initialize client object to an MBrace cluster
let cluster = Config.GetCluster() 

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
cluster.ShowWorkers()

(** Now execute your first cloud workflow and get a handle to the running job: *)
let task = 
    cloud { return "Hello world!" } 
    |> cluster.CreateProcess

// You can get details for the task.
task.ShowInfo()

// Block until the result is computed by the cluster
let text = task.Result

(** Alternatively we can do this all in one line: *)
let quickText = 
    cloud { return "Hello world!" } 
    |> cluster.Run

// You can view the history of processes:
cluster.ShowProcesses()

(** To check that you are running in the cloud, compare a workflow by running locally 
(using async semantics) with one using remote execution. (Note, if using Thespian, these will 
be identical since your cloud is simulated.) *)
let localResult =
    cloud { printfn "hello, world" ; return Environment.MachineName }
    |> cluster.RunLocally

let remoteResult =
    cloud { printfn "hello, world" ; return Environment.MachineName }
    |> cluster.Run

(** 

## Controlling the Cluster

In case you run into trouble, this can be used to clear all process 
records in the cluster: 
*)

cluster.ClearAllProcesses()
