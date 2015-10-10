(*** hide ***)
#load "ThespianCluster.fsx"
//#load "AzureCluster.fsx"

open System
open System.IO
open MBrace.Core
open MBrace.Flow

// Initialize client object to an MBrace cluster
let cluster = Config.GetCluster() 

(**
# Using MBrace.Azure for CPU-intensive work

You now perform a CPU-intensive cloud-parallel workload on your MBrace cluster.

Before running, edit credentials.fsx to enter your connection strings.
*)


#load "lib/collections.fsx"
#load "lib/sieve.fsx"
#time "on"

(**

 Now run this work in different ways on the local machine and cluster
 In each case, you calculate a whole bunch of primes.

First, run on your local machine, single-threaded. Performance will depend 
on the spec of your machine. Note that it is possible that your machine is 
more efficient than each individual machine in the cluster.
*)
let locallyComputedPrimes =
    [| for i in 1 .. 30 do
         let primes = Sieve.getPrimes 100000000
         yield sprintf "calculated %d primes: %A" primes.Length primes  |]

(** Next, run in parallel on the cluster, on multiple workers, each single-threaded. This exploits the
    the multiple machines (workers) in the cluster. *)
let clusterPrimesTask =
    [| for i in 1 .. 30 -> 
         cloud { 
            let primes = Sieve.getPrimes 100000000
            return sprintf "calculated %d primes %A on machine '%s'" primes.Length primes Environment.MachineName 
         }
    |]
    |> Cloud.Parallel
    |> cluster.CreateProcess


clusterPrimesTask.ShowInfo()

let clusterPrimes = clusterPrimesTask.Result

(** Alternatively, you could have started 30 independent jobs.  
This can be handy if you want to track each one independently: *)

let jobs =  
    [ for i in 1 .. 30 -> 
         cloud { 
            let primes = Sieve.getPrimes 100000000
            return sprintf "calculated %d primes %A on machine '%s'" primes.Length primes Environment.MachineName 
         }
        |> cluster.CreateProcess ]

let jobResults = 
    [ for job in jobs -> job.Result ]



(** In this tutorial, you've learned how to do simple CPU-intensive work
using MBrace.Azure. Continue with further samples to learn more about the
MBrace programming model.  *)
