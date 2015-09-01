(*** hide ***)
#load "credentials.fsx"


open System
open System.IO
open MBrace.Core
open MBrace.Azure
open MBrace.Flow

(**
# Your First 'Hello World' Computation with MBrace.Azure

First you send a simple computation to an mbrace cluster using F# Interactive scripting.
You can also send computations from a compiled F# project, though using scripting is very 
common with MBrace.

A guide to creating a cluster is [here](http://www.m-brace.net/#try).

> NOTE: Before running, build this solution to get the required nuget packages, and edit credentials.fsx to enter your Azure connection strings.

First connect to the cluster using a configuration to bind to your storage and service bus on Azure.
*)
let cluster = MBraceAzure.GetHandle(config)

// Optionally, attach console logger to client object 
cluster.EnableClientConsoleLogger <- true

// You can connect to the cluster and get details of the workers in the pool:
cluster.ShowWorkerInfo()

// You can view the history of processes:
cluster.ShowProcessInfo()

(** Now execute your first cloud workflow and get a handle to the running job: *)
let job = 
    cloud { return "Hello world!" } 
    |> cluster.CreateProcess

// You can get details for the process.
job.ShowInfo()

// Block until the result is computed by the cluster
let text = job.Result

(** Alternatively we can do this all in one line: *)
let quickText = 
    cloud { return "Hello world!" } 
    |> cluster.Run

(** To check that you are running in the cloud, compre
a workflow by running locally (using async semantics) with one
using remote execution: *)
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

(**
If you need to get really heavy, you can reset the cluster, which clears 
all process state in queues and storage. Other storage is left unchanged.
Your worker roles may need to be manually rebooted (e.g. from the Azure 
management console).
*)
// cluster.Reset()

(** You can add your local machine to be a worker in the cluster. *)

// MBraceAzure.SpawnLocal(config, 4)

(** You can optionally look at worker (not client) logs for the last 5 minutes. *)

// cluster.ShowSystemLogs(300.0)

(** In this tutorial, you've run your very first work on an MBrace cluster
and learned the basics of controlling the cluster programatically.
You can also control the cluster from the Azure management portal.
Continue with further samples to learn more about the
MBrace programming model.  *)
