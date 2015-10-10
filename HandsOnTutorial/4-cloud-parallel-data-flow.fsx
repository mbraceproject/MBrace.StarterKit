(*** hide ***)
#load "ThespianCluster.fsx"
//#load "AzureCluster.fsx"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit credentials.fsx to enter your connection strings.

open System
open System.IO
open MBrace.Core
open MBrace.Flow

// Initialize client object to an MBrace cluster
let cluster = Config.GetCluster() 


(**
# Using Cloud Flows

You now learn the CloudFlow programming model, for cloud-scheduled
parallel data flow tasks.  This model is similar to Hadoop and Spark.
 
*)
#load "lib/sieve.fsx"

(**
CloudFlow.ofArray partitions the input array based on the number of 
available workers.  The parts of the array are then fed into cloud tasks
implementing the map and filter stages.  The final 'countBy' stage is
implemented by a final cloud task. 
*)

let inputs = [| 1..100 |]

let streamComputationTask = 
    inputs
    |> CloudFlow.OfArray
    |> CloudFlow.map (fun num -> num * num)
    |> CloudFlow.map (fun num -> num % 10)
    |> CloudFlow.countBy id
    |> CloudFlow.toArray
    |> cluster.Submit

(**
Check progress - note the number of cloud tasks involved, which
should be the number of workers + 1.  This indicates
the input array has been partitioned and the work carried out 
 in a distributed way.
*)
streamComputationTask.ShowInfo()

(** Await the result *)
streamComputationTask.Result

(** 

Data parallel cloud flows can be used for all sorts of things.
Later, you will see how to source the inputs to the data flow from
a collection of cloud files, or from a partitioned cloud vector.

For now, you use CloudFlow to do some CPU-intensive work. 
Once again, you compute primes, though you can replace this with
any CPU-intensive computation, using any DLLs on your disk. 

*)

(**

## Changing the degree of parallelism

The default is to partition the input array between all available workers.

You can also use CloudFlow.withDegreeOfParallelism to specify the degree
of partitioning of the stream at any point in the pipeline.
*)
let numbers = [| for i in 1 .. 30 -> 50000000 |]

let computePrimesTask = 
    numbers
    |> CloudFlow.OfArray
    |> CloudFlow.withDegreeOfParallelism 6
    |> CloudFlow.map (fun n -> Sieve.getPrimes n)
    |> CloudFlow.map (fun primes -> sprintf "calculated %d primes: %A" primes.Length primes)
    |> CloudFlow.toArray
    |> cluster.Submit 

(** Check if the work is done *) 
computePrimesTask.ShowInfo()

(**  Await for the result *) 
let computePrimes = computePrimesTask.Result

(**

## Persisting intermediate results to cloud storage

Results of a flow computation can be persisted to store by terminating
with a call to CloudFlow.persist/persistaCached. 
This creates a PersistedCloudFlow instance that can be reused without
performing recomputations of the original flow.

*)

let persistedCloudFlow =
    inputs
    |> CloudFlow.OfArray
    |> CloudFlow.collect(fun i -> seq {for j in 1 .. 10000 -> (i, string j) })
    |> CloudFlow.groupBy snd
    |> CloudFlow.persist StorageLevel.MemoryAndDisk
    |> cluster.Run


let length = persistedCloudFlow |> CloudFlow.length |> cluster.Run
let max = persistedCloudFlow |> CloudFlow.maxBy fst |> cluster.Run

(** In this tutorial, you've learned the basics of the CloudFlow programming
model, a powerful data-flow model for scalable pipelines of data. 
Continue with further samples to learn more about the
MBrace programming model.  *)
