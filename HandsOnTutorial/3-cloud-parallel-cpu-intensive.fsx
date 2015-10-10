(*** hide ***)
#load "ThespianCluster.fsx"
//#load "AzureCluster.fsx"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit AzureCluster.fsx to enter your connection strings.

open System
open System.IO
open MBrace.Core
open MBrace.Flow

// Initialize client object to an MBrace cluster
let cluster = Config.GetCluster() 


#load "lib/collections.fsx"
#load "lib/sieve.fsx"

(**
# Using MBrace.Azure for CPU-intensive work

In this tutorial you learn how to perform CPU-intensive cloud-parallel workload on your MBrace cluster.
The work will be computing prime numbers, though you can easily replace this with any code
of your own.

First, you run some work on your local machine (single-threaded). Performance will depend 
on the spec of your machine. 

> Note that it is possible that your client scripting machine is more efficient than each individual machine in the cluster.
*)
let locallyComputedPrimes =
    [| for i in 1 .. 30 do
         let primes = Sieve.getPrimes 100000000
         yield sprintf "calculated %d primes: %A" primes.Length primes  |]

(** Next, you run the work on the cluster, on multiple workers. This exploits the
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

(** 

## Starting multiple jobs

The previous example used ``Cloud.Parallel`` to compose jobs into one combined job.

Alternatively, you could have started 30 independent jobs.  
This can be handy if you want to track each one independently: 
*)

let jobs =  
    [ for i in 1 .. 30 -> 
         cloud { 
            let primes = Sieve.getPrimes 100000000
            return sprintf "calculated %d primes %A on machine '%s'" primes.Length primes Environment.MachineName 
         }
        |> cluster.CreateProcess ]

let jobResults = 
    [ for job in jobs -> job.Result ]



(** 
## Summary

In this tutorial, you've learned how to do simple CPU-intensive work
using MBrace.Azure. Continue with further samples to learn more about the
MBrace programming model.  

> Note, you can use the above techniques from both scripts and compiled projects. To see the components referenced 
> by this script, see [MBrace.Thespian.fsx](MBrace.Thespian.html) or [MBrace.Azure.fsx](MBrace.Azure.html).
*)
