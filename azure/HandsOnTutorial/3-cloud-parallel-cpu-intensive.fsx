#load "credentials.fsx"
#load "lib/collections.fsx"
#load "lib/sieve.fsx"

open System
open System.IO
open MBrace
open MBrace.Azure
open MBrace.Azure.Client
open MBrace.Workflows
open MBrace.Flow

(**
 You now perform a CPU-intensive cloud-parallel workload on your MBrace cluster.

 Before running, edit credentials.fsx to enter your connection strings.
**)


// First connect to the cluster
let cluster = Runtime.GetHandle(config)

//---------------------------------------------------------------------------
// Specify some work 

#time "on"

// Specifies 30 jobs, each computing all the primes up to 100 million 
let numbers = [| for i in 1 .. 30 -> 100000000 |]


(**

 Now run this work in different ways on the local machine and cluster

 In each case, you calculate a whole bunch of primes.

**)


// Run on your local machine, single-threaded.
//
// Performance will depend on the spec of your machine. Note that it is possible that 
// your machine is more efficient than each individual machine in the cluster.
let locallyComputedPrimes =
    numbers
    |> Array.map(fun num -> 
         let primes = Sieve.getPrimes num
         sprintf "calculated %d primes: %A" primes.Length primes )

// Run in parallel on the cluster, on multiple workers, each single-threaded. This exploits the
// the multiple machines (workers) in the cluster.
//
// Sample time: Real: 00:00:16.269, CPU: 00:00:02.906, GC gen0: 47, gen1: 44, gen2: 1
let clusterPrimesJob =
    numbers
    |> Array.map(fun num -> 
         cloud { let primes = Sieve.getPrimes num
                 return sprintf "calculated %d primes %A on machine '%s'" primes.Length primes Environment.MachineName })
    |> Cloud.Parallel
    |> cluster.CreateProcess


clusterPrimesJob.ShowInfo()

let clusterPrimes = clusterPrimesJob.AwaitResult()

