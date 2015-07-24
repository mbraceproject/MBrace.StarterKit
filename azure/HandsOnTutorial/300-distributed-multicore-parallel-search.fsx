#load "credentials.fsx"
#load "lib/collections.fsx"
#load "lib/mersenne.fsx"

open System
open System.IO
open MBrace.Core
open MBrace.Azure
open MBrace.Azure.Client
open MBrace.Flow

(**
 In this tutorial you learn how to define a new cloud combinator that
 does a nondeterministic, distributed, multi-core parallel search, 
 dividing work first by the number of workers in the cluster, and 
 secondly by the number of cores on each worker.
  
 Before running, edit credentials.fsx to enter your connection strings.
**)

(** First you connect to the cluster: *)
let cluster = Runtime.GetHandle(config)

/// Distributed tryFind combinator with multicore balancing.
///
/// Searches the given array non-deterministically using divide-and-conquer,
/// first dividing according to the number of available workers, and then
/// according to the number of available cores, and then performing sequential
/// search on each machine.
let distributedMultiCoreTryFind (predicate : 'T -> bool) (array : 'T[]) =

    // A local function to do local multicore parallel search
    let localMultiCoreTryFind ts = 
        local {
            // Divide inputs by processor count and evaluate using Local.Choice
            let coreCount = Environment.ProcessorCount
            let tss = Array.splitInto coreCount ts
            return!
                tss
                |> Array.map (fun ts -> local { return Array.tryFind predicate ts })
                |> Local.Choice
        }
    
    // The distributed parallel search, using the local function on each worker
    cloud {
        // Divide inputs by cluster size and evaluate using Parallel.Choice
        let! workerCount = Cloud.GetWorkerCount()
        let tss = Array.splitInto workerCount array
        return!
            tss
            |> Array.map localMultiCoreTryFind
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


