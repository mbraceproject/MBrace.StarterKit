#load "credentials.fsx"
#load "lib/sieve.fsx"

open System
open System.IO
open MBrace
open MBrace.Azure
open MBrace.Azure.Client
open MBrace.Workflows
open MBrace.Flow


(**
 This tutorial illustrates using the CloudFlow programming model that is part of MBrace for cloud-scheduled
 streamed data flow tasks.
 
 Before running, edit credentials.fsx to enter your connection strings.
**)

// First connect to the cluster
let cluster = Runtime.GetHandle(config)

// Parallel distributed data workflows. 
//
// CloudFlow.ofArray partitions the input array based on the number of 
// available workers.
let streamComputationJob = 
    [| 1..100 |]
    |> CloudFlow.ofArray
    |> CloudFlow.map (fun num -> num * num)
    |> CloudFlow.filter (fun num -> num < 2500)
    |> CloudFlow.map (fun num -> if num % 2 = 0 then "Even" else "Odd")
    |> CloudFlow.countBy id
    |> CloudFlow.toArray
    |> cluster.CreateProcess

// Check progress
streamComputationJob.ShowInfo()

// Look at the result
streamComputationJob.AwaitResult()

(** 

 Do some more serious work. Primes! More Primes!

**)

let numbers = [| for i in 1 .. 30 -> 50000000 |]

// The default is to partition the input array between all available workers.
//
// You can also use CloudFlow.withDegreeOfParallelism to specify the degree
// of partitioning of the stream at any point in the pipeline.
let computePrimesJob = 
    numbers
    |> CloudFlow.ofArray
    |> CloudFlow.map Sieve.getPrimes
    |> CloudFlow.map (fun primes -> sprintf "calculated %d primes: %A" primes.Length primes)
    |> CloudFlow.toArray
    |> cluster.CreateProcess // alteratively you can block on the result using cluster.Run

// Check if the work is done
computePrimesJob.ShowInfo()

// Wait for the result
let computePrimes = computePrimesJob.AwaitResult()

