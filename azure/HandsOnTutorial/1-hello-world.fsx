(*** hide ***)
#load "credentials.fsx"


(**
# Your First Cloud Work

First you send a simple computation to an mbrace cluster using F# Interactive scripting.
You can also send computations from a compiled F# project, though using scripting is very 
common with MBrace.

A guide to creating the cluster is [here](https://github.com/mbraceproject/MBrace.StarterKit/blob/master/azure/brisk-tutorial.md#get-started-with-brisk).

> NOTE: Before running, build this solution to get the required nuget packages, and edit credentials.fsx to enter your Azure connection strings.

 **)

open System
open System.IO
open MBrace.Core
open MBrace.Azure
open MBrace.Azure.Client
open MBrace.Flow


(**

First connect to the cluster using a configuration to bind to your storage and service bus on Azure.

Before running, edit credentials.fsx to enter your connection strings.
*)
let cluster = Runtime.GetHandle(config)

(** Optionally, attach console logger to client object *)
cluster.AttachClientLogger(new ConsoleLogger())

(** You can connect to the cluster and get details of the workers in the pool: *)
cluster.ShowWorkers()

(** You can view the history of processes: *)
cluster.ShowProcesses()

(** Now execute your first cloud workflow and get a handle to the running job: *)
let job = 
    cloud { return "Hello world!" } 
    |> cluster.CreateProcess

(** You can evaluate helloWorldProcess to get details on it: *)
let isJobComplete = job.Completed

(** Block until the result is computed by the cluster: *)
let text = job.AwaitResult()

(** Alternatively we can do this all in one line: *)
let quickText = 
    cloud { return "Hello world!" } 
    |> cluster.Run

(** You can test a workflow by running locally using async semantics: *)
let localResult =
    cloud { printfn "hello, world" ; return Environment.MachineName }
    |> cluster.RunLocally

(** Now compare the behaviour against remote execution: *)
let remoteResult =
    cloud { printfn "hello, world" ; return Environment.MachineName }
    |> cluster.Run

(** This can be used to clear all process records in the cluster: *)

// cluster.ClearAllProcesses()

(**
If you need to get really heavy, you can reset the cluster, which clears 
all process state in queues and storage. Other storage is left unchanged.
Your worker roles may need to be manually rebooted (e.g. from the Azure 
management console).
*)
// cluster.Reset()

(** You can add your local machine to be a worker in the cluster. *)

// cluster.AttachLocalWorker()

(** You can optionally look at worker (not client) logs for the last 5 minutes. *)

// cluster.ShowLogs(300.0)
