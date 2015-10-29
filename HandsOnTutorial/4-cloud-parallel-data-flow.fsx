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

#load "lib/sieve.fsx"


(**
# Introduction to Data Parallel Cloud Flows

You now learn the CloudFlow programming model, for cloud-scheduled
parallel data flow tasks.  This model is similar to Hadoop and Spark.
 
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
    |> cluster.CreateProcess

(**
Next, check the progress of your job.

> Note: the number of cloud tasks involved, which should be the number of workers * 2.  This indicates
> the input array has been partitioned and the work carried out in a distributed way.
*)
streamComputationTask.ShowInfo()

(** Next, await the result *)
streamComputationTask.Result

(** 

Data parallel cloud flows can be used for all sorts of things.
Later, you will see how to source the inputs to the data flow from
a collection of cloud files, or from a partitioned cloud vector.


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
    |> cluster.CreateProcess 

(** Next, check if the work is done *) 
computePrimesTask.ShowInfo()

(** Next, await the result *) 
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
    |> CloudFlow.collect(fun i -> seq {for j in 1 .. 10000 -> (i+j, string j) })
    |> CloudFlow.persist StorageLevel.Memory
    |> cluster.Run


let length = persistedCloudFlow |> CloudFlow.length |> cluster.Run
let max = persistedCloudFlow |> CloudFlow.maxBy fst |> cluster.Run

(** 
## Summary

In this tutorial, you've learned the basics of the CloudFlow programming
model, a powerful data-flow model for scalable pipelines of data. 
Continue with further samples to learn more about the
MBrace programming model. 

> Note, you can use the above techniques from both scripts and compiled projects. To see the components referenced 
> by this script, see [MBrace.Thespian.fsx](MBrace.Thespian.html) or [MBrace.Azure.fsx](MBrace.Azure.html).
 *)
