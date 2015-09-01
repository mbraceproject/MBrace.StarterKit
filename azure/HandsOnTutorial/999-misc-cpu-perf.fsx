#load "credentials.fsx"
#load "lib/collections.fsx"
#load "lib/sieve.fsx"

open System
open System.IO
open MBrace.Core
open MBrace.Azure
open MBrace.Flow

(**

 More advanced comparisons.

 In each case, you still calculate a whole bunch of primes.

*)

(** First you connect to the cluster: *)
let cluster = MBraceAzure.GetHandle(config)

let getThread () = System.Threading.Thread.CurrentThread.ManagedThreadId

let numbers = [|1 .. 10000|]

// Run in the cluster, single threaded, on a single random worker.
//
// This doesn't exploit the multiple worker nor multiple cores in the cluster, but gives you an
// idea of the raw performance of the machines in the  cluster. Performance
// will depend on the specification of your machines in the cluster.
//
// Sample time: Real: 00:00:42.830, CPU: 00:00:03.843, GC gen0: 72, gen1: 11, gen2: 0
let clusterSingleMachineSingleThreaded =
    cloud { 
     return numbers |> Array.map(fun n -> 
             let primes = Sieve.getPrimes n 
             sprintf "calculated %d primes: %A on thread %d" primes.Length primes (getThread()))
     } |>  cluster.Run


// Run in the cluster, on a single randome worker, multi-threaded. This exploits the
// mutli-core nature of a single random machine in the cluster.  Performance
// will depend on the specification of your machines in the cluster.
//
// Sample time: Real: 00:00:24.236, CPU: 00:00:03.000, GC gen0: 53, gen1: 10, gen2: 0
let clusterSingleWorkerMultiThreaded =
    cloud { 
     return 
       numbers
       |> Array.splitInto System.Environment.ProcessorCount
       |> Array.Parallel.collect(fun nums -> 
         [| for n in nums do 
             let primes = Sieve.getPrimes n 
             yield sprintf "calculated %d primes: %A on thread %d" primes.Length primes (getThread()) |])
     } |>  cluster.Run

// Run in the cluster, on multiple workers, each multi-threaded. This exploits the
// the multiple machines (workers) in the cluster and each worker is running multi-threaded.
//
// We do the partitioning up-front.  
//
// Sample time: Real: 00:00:11.475, CPU: 00:00:01.921, GC gen0: 22, gen1: 12, gen2: 0
let clusterMultiWorkerMultiThreaded = 
    cloud {
        let! clusterWorkerCount = Cloud.GetWorkerCount()
        return!
            numbers
            |> Array.splitInto clusterWorkerCount
            |> Array.map(fun nums -> 
                 cloud { 
                   return
                       nums
                       |> Array.splitInto System.Environment.ProcessorCount
                       |> Array.Parallel.collect(fun nums -> 
                         [| for n in nums do 
                             let primes = Sieve.getPrimes n 
                             yield sprintf "calculated %d primes: %A on thread %d" primes.Length primes (getThread()) |])
                  })
            |> Cloud.Parallel
        } |> cluster.Run