#load "credentials.fsx"

open System
open System.IO
open MBrace
open MBrace.Azure
open MBrace.Azure.Client
open MBrace.Workflows
open MBrace.Flow

(**
 This demo shows how to start performing distributed workloads on MBrace clusters.

 Before running, edit credentials.fsx to enter your connection strings.
 **)

// First connect to the cluster
let cluster = Runtime.GetHandle(config)

// You now use Cloud.Parallel to run 50 cloud workflows in parallel using a 
// fork-join pattern.
let resultsJob = 
    [ 1 .. 50 ] 
    |> List.map(fun i -> cloud { return sprintf "i'm job %d" i })
    |> Cloud.Parallel
    |> cluster.CreateProcess

cluster.ShowProcesses()

// Get the results
let results = resultsJob.AwaitResult()

// Again, in shorthand
let quickResults =
    [ 1 .. 50 ]
    |> List.map(fun i -> cloud { return sprintf "i'm job %d" i })
    |> Cloud.Parallel
    |> cluster.Run

// Next you use Cloud.Choice: the first cloud workflow to return "Some" wins.
let searchJob =
    [ 1 .. 50 ]
    |> List.map(fun i -> cloud { if i % 10 = 0 then return Some i else return None } )
    |> Cloud.Choice
    |> cluster.CreateProcess

searchJob.ShowInfo()

// Get the result of the search
let searchResult = searchJob.AwaitResult()
