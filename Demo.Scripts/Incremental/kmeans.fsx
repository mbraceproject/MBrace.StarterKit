#load "../../packages/MBrace.Runtime.0.5.6-alpha/bootstrap.fsx" 

open Nessos.MBrace
open Nessos.MBrace.Client

// kmeans implementation ; enter comments here

open System
open System.Text
open System.IO
open System.Threading
open System.Globalization

Thread.CurrentThread.CurrentCulture <- CultureInfo.InvariantCulture

type Point = float[]

let folder = Path.Combine(Path.GetTempPath(), "kmeansFiles")
let fileno = 12


// This function partitions an array into n arrays.
let partition n (a : _ array) =
    [| for i in 0 .. n - 1 ->
        let i, j = a.Length * i / n, a.Length * (i + 1) / n
        Array.sub a i (j - i) |]

// This function calculates the distance between two points.
let dist (arr1 : Point) (arr2 : Point) = Array.fold2 (fun acc elem1 elem2 -> acc + pown (elem1 - elem2) 2) 0.0 arr1 arr2

// This function assigns a point to the correct centroid, and returns the index of that centroid.
let findCentroid (p : Point) (centroids : Point[]) : int =
    let mutable mini = 0
    let mutable min = Double.MaxValue
    for i in 0..(centroids.Length - 1) do
        let dist = dist p centroids.[i]
        if dist < min then
            min <- dist
            mini <- i

    mini

// This function, given a portion of the points, calculates the number of the points assigned to each centroid,
// as well as their sum.
let kmeansLocal (points : ICloudSeq<Point>) (centroids : Point[]) (dim : int) : (int * (int * Point))[] =
    let lens = Array.init centroids.Length (fun _ -> 0)
    let sums : Point[] = Array.init centroids.Length (fun _ -> Array.init centroids.[0].Length (fun _ -> 0.0 ))
    for point in points do
        let cent = findCentroid point centroids
        lens.[cent] <- lens.[cent] + 1
        for i in 0..(point.Length - 1) do
            sums.[cent].[i] <- sums.[cent].[i] + point.[i]

    Array.init centroids.Length (fun i -> (i, (lens.[i], sums.[i])))


// The function runs the kmeans algorithm and returns the centroids of an array of Cloud Sequences of points.
[<Cloud>]
let rec kmeans (points : ICloudSeq<Point>[]) (centroids : Point[]) (iteration : int) : Cloud<Point[]> =
    let sumPoints (pointArr : seq<Point>) dim : Point =
        pointArr
        |> Seq.fold (fun acc elem -> let x = Array.map2 (+) acc elem in x) (Array.init dim (fun _ -> 0.0))

    let divPoint (point : Point) (x : float) : Point =
        Array.map (fun p -> p / x) point


    cloud {
        do! Cloud.Logf "%d: %A" iteration centroids
        let dim = centroids.[0].Length

        let! clusterParts =
            points
            |> Array.map (fun part -> cloud { return kmeansLocal part centroids dim })
            |> Cloud.Parallel

        let newCent =
            clusterParts
            |> Seq.concat
            |> Seq.groupBy fst
            |> Seq.map snd
            |> Seq.map (fun clp ->
                clp
                |> Seq.fold (fun (accN, accSum) (_, (n, sum)) -> (accN + n, sumPoints [|accSum; sum|] dim)) (0, Array.init dim (fun _ -> 0.0))
            )
            |> Seq.map (fun (n, sum) -> divPoint sum (float n))
            |> Seq.toArray

        if Array.forall2 (Array.forall2 (fun x y -> abs(x - y) < 1E-10)) newCent centroids then
            return centroids
        else
            return! kmeans points newCent (iteration + 1)
    }

// The k argument of the kmeans algorithm, and the dimension of points.
let k = 3
let dim = 2

// This function converts a sequence of 'a to a sequence of 'a arrays with dimension dim.
let dimmed (dim : int) (inseq : seq<'a>) : seq<'a[]> =
    let enum = inseq.GetEnumerator()
    seq {
        let hasNext = ref <| enum.MoveNext()
        while !hasNext do
            yield seq {
                let i = ref 0
                while !hasNext && !i < dim do
                    yield enum.Current
                    hasNext := enum.MoveNext()
                    i := !i + 1
                if !i < dim then
                    for j = !i to dim - 1 do
                        yield Unchecked.defaultof<'a>
            }
            |> Seq.toArray
    }

// this function creates files with pseudo random content, in a deterministic way. (12 files =~ 512MB)
let createFiles intArray folder =
    for i in intArray do
        let r = System.Random(i);

        use tw = new StreamWriter(Path.Combine(folder, string i + ".txt")) 
        [|1..64|]
        |> Seq.map (fun _ ->
            let b = StringBuilder()

            [|1..40000|]
            |> Seq.iter (fun _ -> b.Append(r.NextDouble() * 40.0 - 20.0).Append(" ") |> ignore)
    
            b.ToString()
        )
        |> Seq.iter tw.WriteLine
        tw.Flush()
        tw.Close()

Directory.CreateDirectory(folder)
createFiles [|1..fileno|] folder


let parseFile name =
    File.ReadLines name
    |> Seq.collect (fun line -> line.Trim().Split(' '))
    |> Seq.filter(fun x -> x <> "")
    |> Seq.map (float)
    |> dimmed dim

let refs =
    [|1..fileno|]
    |> partition 4
    |> Array.Parallel.map (
        Array.map (fun i ->
            Path.Combine(folder, sprintf "%d.txt" i)
            |> parseFile
            |> StoreClient.Default.CreateCloudSeq
        )
    )
    |> Array.concat

Directory.Delete(folder, true)

let centroids =
    refs.[0]
    |> Seq.take k
    |> Seq.toArray

let runtime = MBrace.InitLocal(totalNodes = 3)

let proc = runtime.CreateProcess <@ kmeans refs centroids 0 @>

runtime.ShowInfo(true)
runtime.ShowProcessInfo()
proc.ShowLogs()

let res = proc.AwaitResult()