(*** hide ***)
#load "../ThespianCluster.fsx"
//#load "../AzureCluster.fsx"

#load "../../packages/FSharp.Charting/FSharp.Charting.fsx"
#r "../../packages/FSharp.Control.AsyncSeq/lib/net45/FSharp.Control.AsyncSeq.dll"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit AzureCluster.fsx to enter your connection strings.

open System
open Nessos.Streams
open MBrace.Core
open FSharp.Charting


// Initialize client object to an MBrace cluster
let cluster = Config.GetCluster() 

(**

# Example: Cloud-distributed k-means clustering with incremental notifications

> This example is from the [MBrace Starter Kit](https://github.com/mbraceproject/MBrace.StarterKit).

This example shows how to implement the iterative algorithm k-Means, which finds centroids of clusters for points.

It shows some important techniques

* How to partition data and keep affinity of workers to data

* How to emit partial results to an intermediate queue

* How to observe that queue using incremental charting

First define some parameters for the input set we want to classify: 
*)


let dim = 2 // point dimensions: we use 2 dimensions so we can chart the results
let numCentroids = 5 // The number of centroids to find
let partitions = 12 // The number of point partitions
let pointsPerPartition = 50000 // The number of points per partition
let epsilon = 0.1

(** Generate some random input data, a deterministic set of points based on the parameters above. *)


/// Represents a multi-dimensional point.
type Point = float[]

/// Generates a set of points via a random walk from the origin, using provided seed.
let generatePoints dim numPoints seed : Point[] =
    let rand = Random(seed * 2003 + 22)
    let prev = Array.zeroCreate dim

    let nextPoint () =
        let arr = Array.zeroCreate dim
        for i = 0 to dim - 1 do 
            arr.[i] <- prev.[i] + rand.NextDouble() * 40.0 - 20.0
            prev.[i] <- arr.[i]
        arr

    [| for i in 1 .. numPoints -> nextPoint() |]

let randPoints = Array.init partitions (generatePoints dim pointsPerPartition)

(** Next you display a chart showing the first 500 points from each partition: *)

let point2d (p:Point) = p.[0], p.[1]

let selectionOfPoints = 
    [ for points in randPoints do 
         for i in 0 .. 100 .. points.Length-1 do
             yield point2d points.[i] ]

Chart.Point selectionOfPoints 

(** 
Giving ![Input to KMeans](../img/kmeans-input.png)

Now you define a set of helper functions and types related to points and finding centroids: 
*)


[<AutoOpen>]
module KMeansHelpers =

    /// Calculates the distance between two points.
    let dist (p1 : Point) (p2 : Point) = 
        Array.fold2 (fun acc e1 e2 -> acc + pown (e1 - e2) 2) 0.0 p1 p2

    /// Assigns a point to the correct centroid, and returns the index of that centroid.
    let findCentroid (p: Point) (centroids: Point[]) : int =
        let mutable mini = 0
        let mutable min = Double.PositiveInfinity
        for i = 0 to centroids.Length - 1 do
            let dist = dist p centroids.[i]
            if dist < min then
                min <- dist
                mini <- i

        mini

    /// Given a set of points, calculates the number of points assigned to each centroid.
    let kmeansLocal (points : Point[]) (centroids : Point[]) : (int * (int * Point))[] =
        let lens = Array.zeroCreate centroids.Length
        let sums = 
            Array.init centroids.Length (fun _ -> Array.zeroCreate centroids.[0].Length)

        for point in points do
            let cent = findCentroid point centroids
            lens.[cent] <- lens.[cent] + 1
            for i = 0 to point.Length - 1 do
                sums.[cent].[i] <- sums.[cent].[i] + point.[i]

        Array.init centroids.Length (fun i -> (i, (lens.[i], sums.[i])))

    /// Sums a collection of points
    let sumPoints (pointArr : Point []) dim : Point =
        let sum = Array.zeroCreate dim
        for p in pointArr do
            for i = 0 to dim - 1 do
                sum.[i] <- sum.[i] + p.[i]
        sum

    /// Scalar division of a point
    let divPoint (point : Point) (x : float) : Point =
        Array.map (fun p -> p / x) point

(** 
This is the iterative computation.  Computes the new centroids based on classifying each point to an existing centroid. 
Then computes new centroids based on that classification.  `emit` is used to emit observations of 
intermediate states to a queue or some other sink.
*)
   
let rec KMeansCloudIterate (partitionedPoints, epsilon, centroids, iteration, emit) = cloud {

     // Stage 1: map computations to each worker per point partition
    let! clusterParts =
        partitionedPoints
        |> Array.map (fun (p:CloudArray<_>, w) -> cloud { return kmeansLocal p.Value centroids }, w)
        |> Cloud.Parallel

    // Stage 2: reduce computations to obtain the new centroids
    let dim = centroids.[0].Length
    let newCentroids =
        clusterParts
        |> Array.concat
        |> ParStream.ofArray
        |> ParStream.groupBy fst
        |> ParStream.sortBy fst
        |> ParStream.map snd
        |> ParStream.map (fun clp -> clp |> Seq.map snd |> Seq.toArray |> Array.unzip)
        |> ParStream.map (fun (ns,points) -> Array.sum ns, sumPoints points dim)
        |> ParStream.map (fun (n, sum) -> divPoint sum (float n))
        |> ParStream.toArray

    // Stage 3: check convergence and decide whether to continue iteration
    let diff = Array.map2 dist newCentroids centroids |> Array.max

    do! Cloud.Logf "KMeans: iteration [#%d], diff %A with centroids /n%A" iteration diff centroids

    // emit an observation
    emit(DateTimeOffset.UtcNow,iteration,diff,centroids)

    if diff < epsilon then
        return newCentroids
    else
        return! KMeansCloudIterate (partitionedPoints, epsilon, newCentroids, iteration+1, emit)
}

            
(** The main cloud routine. Partitions the points according to the available workers, then iterates. *)

        

let KMeansCloud(points, numCentroids, epsilon, emit) = cloud {  

    let initCentroids = points |> Seq.concat |> Seq.take numCentroids |> Seq.toArray

    let! workers = Cloud.GetAvailableWorkers()
    do! Cloud.Logf "KMeans: persisting partitioned point data to store."
        
    // Divide the points
    let! partitionedPoints = 
        points 
        |> Seq.mapi (fun i p -> 
            local { 
                // always schedule the same subset of points to the same worker
                // for caching performance gains
                let! ca = CloudValue.NewArray(p, StorageLevel.MemoryAndDisk) 
                return ca, workers.[i % workers.Length] }) 
        |> Local.Parallel

    do! Cloud.Logf "KMeans: persist completed, starting iteration."

    return! KMeansCloudIterate(partitionedPoints, epsilon, initCentroids, 1, emit) 
}


(** 
## Running a test flight of the algorithm 


You can now run a test flight of the algorithm with a drastically increased epsilon value to allow for
more rapid convergence:
*)

let kmeansTask = 
    KMeansCloud(randPoints, numCentroids, epsilon*10000.0, ignore) 
    |> cluster.CreateProcess

(** Take a look at progress *)

cluster.ShowWorkers()
cluster.ShowProcesses()
kmeansTask.ShowLogs()
kmeansTask.ShowInfo()

(** Get the result: *)

let centroids = kmeansTask.Result

(** Now chart a selection of the original points and the overall result *)

Chart.Combine   
    [ Chart.Point(selectionOfPoints)
      Chart.Point(centroids |> Array.map point2d, Color=Drawing.Color.Red) ]



(** 

Giving ![First results from KMeans](../img/kmeans-results-1.png)

## Observing intermediate states of the algorithm 

Frequently when running iterative algorithms or long running processes you will need
to emit information for visualization and inspection of the progress of the algorithm.

To do this, you create a queue to observe the partial output results from the iterations. 
*)

type Observation = DateTimeOffset*int*float*Point[]

let watchQueue =  CloudQueue.New<Observation>()  |> cluster.RunLocally

(** Next, you start the task, emitting observations to the queue: *)

let kmeansTask2 = 
    KMeansCloud(randPoints, numCentroids, epsilon, watchQueue.Enqueue) 
    |> cluster.CreateProcess

(** Take a look at progress *)

kmeansTask2.ShowLogs()
cluster.ShowWorkers()
cluster.ShowProcesses()
kmeansTask2.ShowInfo()

(** Next, you chart the intermediate results as they arrive as an incrementally updating chart: *)


open FSharp.Control

asyncSeq { 
    let centroidsSoFar = ResizeArray()
    while true do
        match watchQueue.TryDequeue() with
        | Some (time, iteration, diff, centroids) -> 
                centroidsSoFar.Add centroids
                let d = [ for centroids in centroidsSoFar do for p in centroids -> point2d p ]
                yield d
                do! Async.Sleep 1000
        | None -> do! Async.Sleep 1000
} |> AsyncSeq.toObservable |> LiveChart.Point 



(** 

This produces the following incrementally:

![Incremental results from KMeans](../img/kmeans-results-2-incremental.png)

Now wait for the overall result:

*)

let centroids2 = kmeansTask2.Result

(** Now chart the original points, the centroids we computed on the first flight, and the final centroids. *)

Chart.Combine   
    [ Chart.Point(selectionOfPoints)
      Chart.Point(centroids |> Array.map point2d, Color=Drawing.Color.Orange,MarkerSize=10) 
      Chart.Point(centroids2 |> Array.map point2d, Color=Drawing.Color.Red,MarkerSize=5) ]

(** 

Giving 

![The centroids found by the clustering](../img/kmeans-results-2.png)

In this example, you've learned how to run an iterative algorithm on an MBrace cluster,
including how to emit and observe intermediate states from the iterations.
Continue with further samples to learn more about the MBrace programming model.   


> Note, you can use the above techniques from both scripts and compiled projects. To see the components referenced 
> by this script, see [MBrace.Thespian.fsx](MBrace.Thespian.html) or [MBrace.Azure.fsx](MBrace.Azure.html).
*)
