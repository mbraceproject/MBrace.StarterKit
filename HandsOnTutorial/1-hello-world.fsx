(*** hide ***)
#load "ThespianCluster.fsx"
//#load "AzureCluster.fsx"

open System
open System.IO
open MBrace.Core
open MBrace.Flow

(**
# Your First 'Hello World' Computation with MBrace

A guide to creating a cluster is [here](http://www.m-brace.net/#try).

Start F# Interactive in your editor.  Highlight the text below and press "Alt-Enter" (Visual Studio) or the other
appropriate execution command for your editor. This connects to the cluster.  If you are using a locally simulated
cluster it also creates the cluster.

*)

let cluster = Config.GetCluster()

(**
Next, get details of the workers in your cluster. Again, highlight the text below and
execute it in your scripting client:
*)    

cluster.ShowWorkers()

(** Now execute your first cloud workflow, returning a handle to the running job: *)
let task = 
    cloud { return "Hello world!" } 
    |> cluster.CreateProcess

(** This submits a task to the cluster. To get details for the task, execute the 
following in your scripting client: *)

task.ShowInfo()

(** Your task is likely complete by now.  To get the result returned by your 
task, execute the following in your scripting client: *)
let text = task.Result

(** Alternatively we can do this all in one line: *)
let quickText = 
    cloud { return "Hello world!" } 
    |> cluster.Run


(** To check that you are running in the cloud, compare a workflow running locally 
with one using cloud execution. (Note, if using an MBrace.Thespian locally simulated
cluster, these will be identical.) *)
let localResult =
    cloud { printfn "hello, world" ; return Environment.MachineName }
    |> cluster.RunLocally

let remoteResult =
    cloud { printfn "hello, world" ; return Environment.MachineName }
    |> cluster.Run

(** 

## Controlling the Cluster

To view the history of processes, execute the following line from your scriptin
*)

cluster.ShowProcesses()

(**
In case you run into trouble, you can clear all process records in the cluster
by executing the following from your scripting client:
*)

cluster.ClearAllProcesses()

(**

> Note, you can use the above techniques from both scripts and compiled projects. To see the components referenced 
> by this script, see [MBrace.Thespian.fsx](MBrace.Thespian.html) or [MBrace.Azure.fsx](MBrace.Azure.html).
**)