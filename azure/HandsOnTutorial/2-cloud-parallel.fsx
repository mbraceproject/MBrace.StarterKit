(*** hide ***)
#load "credentials.fsx"

open System
open System.IO
open MBrace.Core
open MBrace.Azure
open MBrace.Azure.Client
open MBrace.Flow


(**
# Simple Cloud Parallelism with MBrace.Azure

You now perform a very simple parallel distributed job on your MBrace cluster.

Before running, edit credentials.fsx to enter your connection strings.

*)


(** First you connect to the cluster: *)
let cluster = Runtime.GetHandle(config)

(** You now use Cloud.Parallel to run 50 cloud workflows in parallel using fork-join pattern. *)
let resultsJob = 
    [ for i in 1 .. 50 -> cloud { return sprintf "i'm job %d" i } ]
    |> Cloud.Parallel
    |> cluster.CreateProcess

cluster.ShowProcesses()


(** Get the results *)
let results = resultsJob.AwaitResult()


(** Again, in shorthand *)
let quickResults =
    [ for i in 1 .. 50 -> cloud { return sprintf "i'm job %d" i } ]
    |> Cloud.Parallel
    |> cluster.Run

(** Alternatively, you could have started 50 independent jobs.  This can be handy if you want to 
track each one independently: *)

let jobs =  
    [ for i in 1 .. 50 -> 
        cloud { return sprintf "i'm job %d" i } 
        |> cluster.CreateProcess ]

let jobResults = 
    [ for job in jobs -> job.AwaitResult() ]

(** Next you use Cloud.Choice: the first cloud workflow to return "Some" wins. *)
let searchJob =
    [ for i in 1 .. 50 -> cloud { if i % 10 = 0 then return Some i else return None } ]
    |> Cloud.Choice
    |> cluster.CreateProcess

searchJob.ShowInfo()

(** Await the result of the search: *)
let searchResult = searchJob.AwaitResult()
