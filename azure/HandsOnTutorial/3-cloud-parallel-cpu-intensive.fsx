(*** hide ***)
#load "credentials.fsx"

(**
# Using MBrace.Azure for CPU-intensive work

 You now perform a CPU-intensive cloud-parallel workload on your MBrace cluster.

 Before running, edit credentials.fsx to enter your connection strings.
**)

open System
open System.IO
open MBrace.Core
open MBrace.Azure
open MBrace.Azure.Client
open MBrace.Flow


#load "lib/collections.fsx"
#load "lib/sieve.fsx"
#time "on"

(** First you connect to the cluster: *)
let cluster = Runtime.GetHandle(config)


(**

 Now run this work in different ways on the local machine and cluster

 In each case, you calculate a whole bunch of primes.

**)


(** 
First, run on your local machine, single-threaded.

Performance will depend on the spec of your machine. Note that it is possible that 
your machine is more efficient than each individual machine in the cluster.
*)
let locallyComputedPrimes =
    [| for i in 1 .. 30 do
         let primes = Sieve.getPrimes 100000000
         yield sprintf "calculated %d primes: %A" primes.Length primes  |]

(** Next, run in parallel on the cluster, on multiple workers, each single-threaded. This exploits the
    the multiple machines (workers) in the cluster. *)
let clusterPrimesJob =
    [| for i in 1 .. 30 -> 
         cloud { 
            let primes = Sieve.getPrimes 100000000
            return sprintf "calculated %d primes %A on machine '%s'" primes.Length primes Environment.MachineName 
         }
    |]
    |> Cloud.Parallel
    |> cluster.CreateProcess


clusterPrimesJob.ShowInfo()

let clusterPrimes = clusterPrimesJob.AwaitResult()

