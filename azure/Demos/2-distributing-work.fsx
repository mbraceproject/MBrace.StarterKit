#load "credentials.fsx"

open System
open System.IO
open MBrace
open MBrace.Azure
open MBrace.Azure.Client
open MBrace.Azure.Runtime
open MBrace.Streams
open MBrace.Workflows
open Nessos.Streams

(**
 This demo shows how to start performing distributed workloads on MBrace clusters.

 Before running, edit credentials.fsx to enter your connection strings.
 **)

// First connect to the cluster
let cluster = Runtime.GetHandle(config)

// Now we can make 50 jobs and compose them in parallel.
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

// This shows the next composition combinator: Cloud.Choice, which does
// non-deterministic choice among a collection of workers.
let quickSearch =
    [ 1 .. 50 ]
    |> List.map(fun i -> cloud { if i % 10 = 0 then return Some i else return None } )
    |> Cloud.Choice
    |> cluster.Run

