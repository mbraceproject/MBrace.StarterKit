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

# Example: Cloud-distributed k-means clustering 

This example shows how to implement the iterative algorithm k-Means, which finds centroids of clusters for points.

It shows some important techniques

* How to partition data and keep affinity of workers to data

* How to emit partial results to an intermediate queue

* How to observe that queue using incremental charting

First you define a set of helper functions and types related to points and finding centroids: 
*)

/// Represents a multi-dimensional point.
type Point = float[]

[<AutoOpen>]
module KMeansHelpers =

    /// Generates a set of points via a random walk from the origin, using provided seed.
    let generatePoints dim numPoints seed =
        let rand = Random(seed * 2003 + 22)
        let prev = Array.zeroCreate<float> dim

        let nextPoint () : Point =
            let arr = Array.zeroCreate<float> dim
            for i = 0 to dim - 1 do 
                arr.[i] <- prev.[i] + rand.NextDouble() * 40.0 - 20.0
                prev.[i] <- arr.[i]
            arr

        [| for i in 1 .. numPoints -> nextPoint() |]

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


(** Now define some parameters for the input set we want to classify: *)

let dim = 2 // point dimensions: we use 2 dimensions so we can chart the results
let numCentroids = 5 // The k argument of the kmeans algorithm, and the dimension of points.
let partitions = 12 // number of point partitions
let pointsPerPartition = 50000 // number of points per partition
let epsilon = 0.1

(** Generate some random input data, a deterministic set of points based on the parameters above. *)

let randPoints = Array.init partitions (KMeansHelpers.generatePoints dim pointsPerPartition)

(** Next you display a chart showing the first 500 points from each partition: *)

Chart.FastPoint([| for points in randPoints do for p in Seq.take 500 points -> p.[0], p.[1] |] ) 

(** Next, you create a queue to observe the partial output results from the iterations. *)

type Observation = DateTimeOffset*int*float*Point[]

let watchQueue =  CloudQueue.New<Observation>()  |> cluster.RunLocally

(** Next, you start the task, emitting observations to the queue: *)

let kmeansTask = 
    KMeansCloud(randPoints, numCentroids, epsilon, watchQueue.Enqueue) 
    |> cluster.CreateProcess

(** Take a look at progress *)

kmeansTask.ShowLogs()
cluster.ShowWorkers()
cluster.ShowProcesses()
kmeansTask.ShowInfo()

(** Next, you chart the intermediate results as they arrive as an incrementally updating chart: *)


open FSharp.Control

asyncSeq { 
    let centroidsSoFar = ResizeArray()
    while true do
        match watchQueue.TryDequeue() with
        | Some (time, iteration, diff, centroids) -> 
                centroidsSoFar.Add centroids
                let d = [ for centroids in centroidsSoFar do for p in centroids -> p.[0], p.[1] ]
                yield d
                do! Async.Sleep 1000
        | None -> do! Async.Sleep 1000
} |> AsyncSeq.toObservable |> LiveChart.Point 



(** Now wait for the overall result *)

kmeansTask.Result


