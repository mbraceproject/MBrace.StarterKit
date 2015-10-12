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

Alternatively, you could have started 30 independent jobs.  This can be handy if you want to track each one independently: 
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

(** For example, you might want to track the exectuion time of each job: *)
let jobTimes = 
    [ for job in jobs -> job.ExecutionTime ]

(** 

## Tracking and controlling job failures

Jobs can fail for all sorts of reasons, normally by raising exceptions. For example, let's
simulate this by injecting failures into our jobs:

*)

let makeJob i = 
    cloud { 
        if i % 8 = 0 then failwith "fail"
        let primes = Sieve.getPrimes 1000000
        return sprintf "calculated %d primes %A on machine '%s'" primes.Length primes Environment.MachineName 
    }

let jobs2 =  
    [ for i in 1 .. 10 -> 
         makeJob i |> cluster.CreateProcess ]

(** If you now attempt to access the results of the job, you will get the exception propagated back to your client scripting session: 

    // raises an exception since some of the jobs failed
    let jobResults2 = 
        [ for job in jobs2 -> job.Result ]  

When this happens, it is useful to protect your jobs by a wrapper that captures information about the failing work: *)

type Result<'T, 'Info> =
   | Success of 'T
   | Failure of 'Info * exn

let protectJob info (work: Cloud<_>) = 
    cloud { 
       try
           let! result = work 
           return Success result
       with exn -> 
           return Failure (info, exn)
    }

let jobs3 =  
    [ for i in 1 .. 10 -> 
         makeJob i
         |> protectJob ("job " + string i) 
         |> cluster.CreateProcess ]

let jobResults3 = 
    [ for job in jobs3 -> job.Result ]


(** The results will now show the success and failure of each job:

    val jobResults3 : Result<string,string> list =
      [Success "calculated 78498 primes [|2; 3; 5; 7; 11; 13; 17; 19; 23; 29;"+[477 chars];
       Success "calculated 78498 primes [|2; 3; 5; 7; 11; 13; 17; 19; 23; 29;"+[477 chars];
       Success "calculated 78498 primes [|2; 3; 5; 7; 11; 13; 17; 19; 23; 29;"+[477 chars];
       Success "calculated 78498 primes [|2; 3; 5; 7; 11; 13; 17; 19; 23; 29;"+[477 chars];
       Success "calculated 78498 primes [|2; 3; 5; 7; 11; 13; 17; 19; 23; 29;"+[477 chars];
       Success "calculated 78498 primes [|2; 3; 5; 7; 11; 13; 17; 19; 23; 29;"+[477 chars];
       Success "calculated 78498 primes [|2; 3; 5; 7; 11; 13; 17; 19; 23; 29;"+[477 chars];
       Failure ("job 8", System.Exception: fail...)
       Success "calculated 78498 primes [|2; 3; 5; 7; 11; 13; 17; 19; 23; 29;"+[477 chars];
       Success "calculated 78498 primes [|2; 3; 5; 7; 11; 13; 17; 19; 23; 29;"+[477 chars]]

You can then re-create adjusted versions of failing jobs and re-run them:
*)

makeJob 8 |> cluster.Run

(** 
## Summary

In this tutorial, you've learned how to do simple CPU-intensive work
using MBrace.Azure. Continue with further samples to learn more about the
MBrace programming model.  

> Note, you can use the above techniques from both scripts and compiled projects. To see the components referenced 
> by this script, see [MBrace.Thespian.fsx](MBrace.Thespian.html) or [MBrace.Azure.fsx](MBrace.Azure.html).
*)
