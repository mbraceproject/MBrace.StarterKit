#load "credentials.fsx"
#load "lib/sieve.fsx"

open System
open System.IO
open MBrace
open MBrace.Azure
open MBrace.Azure.Client
open MBrace.Workflows
open MBrace.Flow


(**
 You now learn the CloudFlow programming model, for cloud-scheduled
 parallel data flow tasks.  This model is similar to Hadoop, Spark
 and/or Dryad LINQ.
 
 Before running, edit credentials.fsx to enter your connection strings.
**)

// First connect to the cluster
let cluster = Runtime.GetHandle(config)

// Parallel distributed data workflows. 
//
// CloudFlow.ofArray partitions the input array based on the number of 
// available workers.  The parts of the array are then fed into cloud tasks
// implementing the map and filter stages.  The final 'countBy' stage is
// implemented by a final cloud task. 
let streamComputationJob = 
    [| 1..100 |]
    |> CloudFlow.ofArray
    |> CloudFlow.map (fun num -> num * num)
    |> CloudFlow.filter (fun num -> num < 2500)
    |> CloudFlow.map (fun num -> if num % 2 = 0 then "Even" else "Odd")
    |> CloudFlow.countBy id
    |> CloudFlow.toArray
    |> cluster.CreateProcess

// Check progress - note the number of cloud tasks involved, which
// should be the number of workers + 1.  This indicates
// the input array has been partitioned and the work carried out 
// in a distributed way.
streamComputationJob.ShowInfo()

// Look at the result
streamComputationJob.AwaitResult()

(** 

Data parallel cloud flows can be used for all sorts of things.
Later, you will see how to source the inputs to the data flow from
a collection of cloud files, or from a partitioned cloud vector.

For now, you use CloudFlow to do some CPU-intensive work. 
Once again, you compute primes, though you can replace this with
any CPU-intensive computation, using any DLLs on your disk. 

**)

let numbers = [| for i in 1 .. 30 -> 50000000 |]

// The default is to partition the input array between all available workers.
//
// You can also use CloudFlow.withDegreeOfParallelism to specify the degree
// of partitioning of the stream at any point in the pipeline.
let computePrimesJob = 
    numbers
    |> CloudFlow.ofArray
    |> CloudFlow.map (fun n -> Sieve.getPrimes n)
    |> CloudFlow.map (fun primes -> sprintf "calculated %d primes: %A" primes.Length primes)
    |> CloudFlow.toArray
    |> cluster.CreateProcess // alteratively you can block on the result using cluster.Run

// Check if the work is done
computePrimesJob.ShowInfo()

// Wait for the result
let computePrimes = computePrimesJob.AwaitResult()

