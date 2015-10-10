(*** hide ***)
#load "ThespianCluster.fsx"
//#load "AzureCluster.fsx"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit credentials.fsx to enter your connection strings.

open System
open System.IO
open MBrace.Core
open MBrace.Flow

// Initialize client object to an MBrace cluster
let cluster = Config.GetCluster() 

(**
# Simple Cloud Parallelism 

You now perform a very simple parallel distributed job on your MBrace cluster.

*)

(** You now use Cloud.Parallel to run 50 cloud workflows in parallel using fork-join pattern. *)
let resultsTask = 
    [ for i in 1 .. 50 -> cloud { return sprintf "i'm work item %d" i } ]
    |> Cloud.Parallel
    |> cluster.Submit

cluster.ShowProcesses()


(** Get the results *)
let results = resultsTask.Result


(** Again, in shorthand *)
let quickResults =
    [ for i in 1 .. 50 -> cloud { return sprintf "i'm work item %d" i } ]
    |> Cloud.Parallel
    |> cluster.Run

(** Next you use Cloud.Choice: the first cloud workflow to return "Some" wins. *)
let searchTask =
    [ for i in 1 .. 50 -> cloud { if i % 10 = 0 then return Some i else return None } ]
    |> Cloud.Choice
    |> cluster.Submit

searchTask.ShowInfo()

(** Await the result of the search: *)
let searchResult = searchTask.Result

(** In this tutorial, you've learned about `Cloud.Parallel` and `Cloud.Choice`
as ways of composing cloud workflows Continue with further samples to learn more about the
MBrace programming model.  *)
