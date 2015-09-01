(*** hide ***)
#load "credentials.fsx"

open System
open System.IO
open MBrace.Core
open MBrace.Azure
open MBrace.Flow


(**
# Simple Cloud Parallelism with MBrace.Azure

You now perform a very simple parallel distributed job on your MBrace cluster.
Before running, edit credentials.fsx to enter your connection strings.

*)


(** First you connect to the cluster: *)
let cluster = MBraceAzure.GetHandle(config)

(** You now use Cloud.Parallel to run 50 cloud workflows in parallel using fork-join pattern. *)
let resultsJob = 
    [ for i in 1 .. 50 -> cloud { return sprintf "i'm job %d" i } ]
    |> Cloud.Parallel
    |> cluster.CreateProcess

cluster.ShowProcessInfo()


(** Get the results *)
let results = resultsJob.Result


(** Again, in shorthand *)
let quickResults =
    [ for i in 1 .. 50 -> cloud { return sprintf "i'm job %d" i } ]
    |> Cloud.Parallel
    |> cluster.Run

(** Next you use Cloud.Choice: the first cloud workflow to return "Some" wins. *)
let searchJob =
    [ for i in 1 .. 50 -> cloud { if i % 10 = 0 then return Some i else return None } ]
    |> Cloud.Choice
    |> cluster.CreateProcess

searchJob.ShowInfo()

(** Await the result of the search: *)
let searchResult = searchJob.Result

(** In this tutorial, you've learned about `Cloud.Parallel` and `Cloud.Choice`
as ways of composing cloud workflows Continue with further samples to learn more about the
MBrace programming model.  *)
