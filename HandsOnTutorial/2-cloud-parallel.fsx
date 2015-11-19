(*** hide ***)
#load "ThespianCluster.fsx"
//#load "AzureCluster.fsx"

#load "lib/utils.fsx"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit AzureCluster.fsx to enter your connection strings.

open System
open System.IO
open MBrace.Core
open MBrace.Flow

// Initialize client object to an MBrace cluster
let cluster = Config.GetCluster() 

(**
# Introduction to Cloud Combinators

> This tutorial is from the [MBrace Starter Kit](https://github.com/mbraceproject/MBrace.StarterKit).

You now perform a very simple parallel distributed job on your MBrace cluster.

*)

(** You now use Cloud.Parallel to run 10 cloud workflows in parallel using fork-join pattern. *)
let parallelTask = 
    [ for i in 1 .. 10 -> cloud { return sprintf "i'm work item %d" i } ]
    |> Cloud.Parallel
    |> cluster.CreateProcess

cluster.ShowProcesses()


(** Get the results *)
let results = parallelTask.Result

(** 
Cloud.Parallel is not the only combinator for parallelism and for some
items doesn't use all multi-core capacity on machines.
The ``Cloud.ParallelBalanced`` combinator defined in ``lib/utils.fsx`` is
designed for use when you have many single-machine, single-core items to run on multiple
machines using multi-core capacity. 

When specifying individual work items that are constrained
to only execute on a single machine you use ``local { ... }``.  These work
items must commonly be used as inputs to parallelism combinators that
use particular multi-core aware scheduling strategies.
*)
let parallelTask2 = 
    [ for i in 1 .. 10 -> local { return sprintf "i'm work item %d" i } ]
    |> Cloud.ParallelBalanced
    |> cluster.CreateProcess

cluster.ShowProcesses()


(** Get the results *)
let results2 = parallelTask2.Result


(** Again, in shorthand *)
let quickResults =
    [ for i in 1 .. 50 -> local { return sprintf "i'm work item %d" i } ]
    |> Cloud.ParallelBalanced
    |> cluster.Run

(** Next you use Cloud.Choice: the first cloud workflow to return "Some" wins. *)
let searchTask =
    [ for i in 1 .. 50 -> cloud { if i % 10 = 0 then return Some i else return None } ]
    |> Cloud.Choice
    |> cluster.CreateProcess

searchTask.ShowInfo()

(** Await the result of the search: *)
let searchResult = searchTask.Result

(** Like ``Cloud.ParallelBalanced``, you can also use Cloud.ChoiceBalanced, which is multi-core aware. *)
let searchTask2 =
    [ for i in 1 .. 50 -> local { if i % 10 = 0 then return Some i else return None } ]
    |> Cloud.ChoiceBalanced
    |> cluster.CreateProcess

searchTask2.ShowInfo()

(** Await the result of the search: *)
let searchResult2 = searchTask2.Result

(** 

## Summary

In this tutorial, you've learned about `Cloud.Parallel` and `Cloud.Choice`
as ways of composing cloud workflows. You've also learned about `Cloud.ParallelBalanced` and `Cloud.ChoiceBalanced`
defined in `lib/utils.fsx` which utlize the multi-code capacity off your cluster.
Continue with further samples to learn more about the MBrace programming model.  

> Note, you can use the above techniques from both scripts and compiled projects. To see the components referenced 
> by this script, see [ThespianCluster.fsx](ThespianCluster.html) or [AzureCluster.fsx](AzureCluster.html).
*)
