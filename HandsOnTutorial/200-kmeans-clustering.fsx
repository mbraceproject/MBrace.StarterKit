(*** hide ***)
#load "ThespianCluster.fsx"
//#load "AzureCluster.fsx"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit AzureCluster.fsx to enter your connection strings.

open System
open Nessos.Streams
open MBrace.Core

// Initialize client object to an MBrace cluster
let cluster = Config.GetCluster() 

(**

# Simple k-means clustering implementation

*)

/// Represents a multi-dimensional point.
type Point = float[]

[<RequireQualifiedAccess>]
module KMeans =

    // this function creates files with pseudo random content, in a deterministic way. (12 files =~ 512MB)
    /// <summary>
    ///     Generates a set of pseudo random points, using provided seed.
    /// </summary>
    /// <param name="dim">Point dimension.</param>
    /// <param name="numPoints">Number of points to generate.</param>
    /// <param name="seed">Random number generation seed.</param>
    let generatePoints (dim : int) (numPoints : int) (seed : int) : seq<Point> =
        if dim <= 0 || numPoints < 0 then raise <| new ArgumentOutOfRangeException()
        let rand = System.Random(seed)

        let nextPoint _ : Point =
            let arr = Array.zeroCreate<float> dim
            for i = 0 to dim - 1 do arr.[i] <- rand.NextDouble() * 40.0 - 20.0
            arr

        Seq.init numPoints nextPoint

    // Calculates the distance between two points.
    let private dist (p1 : Point) (p2 : Point) = Array.fold2 (fun acc e1 e2 -> acc + pown (e1 - e2) 2) 0.0 p1 p2

    // Assigns a point to the correct centroid, and returns the index of that centroid.
    let private findCentroid (p : Point) (centroids : Point[]) : int =
        let mutable mini = 0
        let mutable min = Double.PositiveInfinity
        for i = 0 to centroids.Length - 1 do
            let dist = dist p centroids.[i]
            if dist < min then
                min <- dist
                mini <- i

        mini

    /// Given a portion of the points, calculates the number of the points assigned to each centroid as well as their sum.
    let private kmeansLocal (points : Point seq) (centroids : Point[]) : (int * (int * Point))[] =
        let lens = Array.init centroids.Length (fun _ -> 0)
        let sums : Point[] = Array.init centroids.Length (fun _ -> Array.init centroids.[0].Length (fun _ -> 0.0 ))
        for point in points do
            let cent = findCentroid point centroids
            lens.[cent] <- lens.[cent] + 1
            for i = 0 to point.Length - 1 do
                sums.[cent].[i] <- sums.[cent].[i] + point.[i]

        Array.init centroids.Length (fun i -> (i, (lens.[i], sums.[i])))

    let private sumPoints (pointArr : Point []) dim : Point =
        pointArr |> Array.fold (fun acc elem -> let x = Array.map2 (+) acc elem in x) (Array.init dim (fun _ -> 0.0))

    let private divPoint (point : Point) (x : float) : Point =
        Array.map (fun p -> p / x) point

    /// Runs the kmeans algorithm and returns the centroids of the given set of points
    let calculate (k : int) (points : seq<#seq<Point>>) : Cloud<Point []> = cloud {
        let rec aux (points : (CloudArray<Point> * IWorkerRef) []) (centroids : Point[]) (iteration : int) : Cloud<Point[]> = cloud {
            do! Cloud.Logf "KMeans: iteration [#%d] with centroids \n %A" iteration centroids

            // Stage 1: map computations to each worker per point partition
            let! clusterParts =
                points
                |> Array.map (fun (part, w) -> cloud { return kmeansLocal part.Value centroids }, w)
                |> Cloud.Parallel

            // Stage 2: reduce computations to obtain the new centroids
            let dim = centroids.[0].Length
            let newCentroids =
                clusterParts
                |> Array.concat
                |> ParStream.ofArray
                |> ParStream.groupBy fst
                |> ParStream.map snd
                |> ParStream.map (fun clp -> clp |> Seq.fold (fun (accN, accSum) (_, (n, sum)) -> (accN + n, sumPoints [|accSum; sum|] dim)) (0, Array.init dim (fun _ -> 0.0)))
                |> ParStream.map (fun (n, sum) -> divPoint sum (float n))
                |> ParStream.toArray

            // Stage 3: check convergence and decide whether to continue iteration
            if Array.forall2 (Array.forall2 (fun x y -> abs(x - y) < 1E-10)) newCentroids centroids then
                return centroids
            else
                return! aux points newCentroids (iteration + 1)
        }

        let initCentroids = points |> Seq.concat |> Seq.take k |> Seq.toArray

        let! workers = Cloud.GetAvailableWorkers()
        do! Cloud.Logf "KMeans: persisting point data to store."
        let! assignedPoints = 
            points 
            |> Seq.mapi (fun i p -> 
                local { 
                    // always schedule the same subset of points to the same worker, for caching performance
                    let! ca = CloudValue.NewArray(p, StorageLevel.MemoryAndDisk) 
                    return ca, workers.[i % workers.Length] }) 
            |> Local.Parallel

        do! Cloud.Logf "KMeans: persist completed, starting iteration."
        return! aux assignedPoints initCentroids 1
    }


let dim = 7 // point dimensions
let k = 5 // The k argument of the kmeans algorithm, and the dimension of points.
let partitions = 12 // number of point partitions
let pointsPerPartition = 500000 // number of points per partition

let randPoints = Array.init partitions (fun seed -> KMeans.generatePoints dim pointsPerPartition seed) // generate deterministic set of points based on the parameters above

let kmeansProc = cluster.CreateProcess (KMeans.calculate k randPoints)

kmeansProc.ShowLogs()
cluster.ShowWorkers()
kmeansProc.ShowInfo()
kmeansProc.Result