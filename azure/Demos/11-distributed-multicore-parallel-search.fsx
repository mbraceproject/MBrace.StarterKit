#load "credentials.fsx"
#load "lib/collections.fsx"
#load "lib/mersenne.fsx"

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
 In this tutorial you learn how to use Cloud.Choice to do a nondeterministic parallel computation using 
 Mersenne prime number searches.
  
 Before running, edit credentials.fsx to enter your connection strings.
**)

// First connect to the cluster
let cluster = Runtime.GetHandle(config)

/// Distributed tryFind combinator with multicore balancing.
///
/// Searches the given array non-deterministically using divide-and-conquer,
/// first dividing according to the number of available workers, and then
/// according to the number of available cores, and then performing sequential
/// search on each machine.
let distributedMultiCoreTryFind (predicate : 'T -> bool) (ts : 'T []) =

    // sequential single-threaded search
    let sequentialTryFind (ts : 'T []) = local { return Array.tryFind predicate ts }

    // local multicore parallel search
    let localmultiCoreTryFind (ts : 'T []) = local {
        if ts.Length <= 1 then return! sequentialTryFind ts
        else
            // Divide inputs by processor count and evaluate using Local.Choice
            let tss = Array.splitInto System.Environment.ProcessorCount ts
            return!
                tss
                |> Array.map sequentialTryFind
                |> Local.Choice
    }
    
    // distributed parallel search
    cloud {
        if ts.Length <= 1 then return! sequentialTryFind ts
        else
            // Divide inputs by cluster size and evaluate using Parallel.Choice
            let! clusterSize = Cloud.GetWorkerCount()
            let tss = Array.splitInto clusterSize ts
            return!
                tss
                |> Array.map localmultiCoreTryFind
                |> Cloud.Choice
    }

#time

/// Known Mersenne exponents : 9,689 and 9,941
let exponentRange = [| 9000 .. 10000 |]

/// Sequential Mersenne prime search
let tryFindMersenneLocal ts = Array.tryFind Primality.isMersennePrime ts

// Execution time = 00:05:46.615, sample local machine
tryFindMersenneLocal exponentRange

/// MBrace distributed, multi-core, nondeterministic Mersenne prime search
let tryFindMersenneCloud ts = distributedMultiCoreTryFind Primality.isMersennePrime ts

// ExecutionTime = 00:00:38.2472020, 3 small instance cluster
let searchJob = tryFindMersenneCloud exponentRange |> cluster.CreateProcess

searchJob.ShowInfo()
cluster.ShowWorkers()

searchJob.AwaitResult()


