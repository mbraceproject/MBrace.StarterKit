(*** hide ***)
#load "../ThespianCluster.fsx"
//#load "AzureCluster.fsx"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit AzureCluster.fsx to enter your connection strings.

open System
open System.Numerics
open System.IO
open System.Text

open Nessos.Streams

open MBrace.Core
open MBrace.Flow

// Initialize client object to an MBrace cluster
let cluster = Config.GetCluster() 

(**

# KNN Digit Recognizer

Digit recognizer classification using k nearest neighbours based on the Kaggle dataset.
https://www.kaggle.com/c/digit-recognizer

*)

[<Literal>]
let pixelLength = 784 // 28 * 28

/// Image identifier
type ImageId = int

/// Image bitmap representation
type Image = 
    { Id : ImageId 
      Pixels : int [] }

    /// Parses a set of points from text using the Kaggle digit recognizer CSV format
    static member Parse (file : string) : Image [] =
        File.ReadAllLines(file)
        |> Stream.ofSeq
        |> Stream.skip 1
        |> Stream.map (fun line -> line.Split(','))
        |> Stream.map (fun line -> line |> Array.map int)
        |> Stream.mapi (fun i nums -> let id = i + 1 in { Id = id ; Pixels = nums })
        |> Stream.toArray

/// Digit classification
type Classification = int

/// Distance on points; use uint64 to avoid overflows
type Distance = Image -> Image -> uint64

/// A training image annotaded by its classification
type TrainingImage = 
    { Classification : Classification 
      Image : Image }

    /// Parses a training set from text using the Kaggle digit recognizer CSV format
    static member Parse(file : string) : TrainingImage [] =
        File.ReadAllLines(file)
        |> Stream.ofSeq
        |> Stream.skip 1
        |> Stream.map (fun line -> line.Split(','))
        |> Stream.map (fun line -> line |> Array.map int)
        |> Stream.mapi (fun i nums -> 
                            let id = i + 1
                            let image = { Id = id ; Pixels = nums.[1..] }
                            { Classification = nums.[0] ; Image = image })
        |> Stream.toArray

/// Digit classifier implementation
type Classifier = TrainingImage [] -> Image -> Classification

type Classifications =

    /// Writes a point classification to file
    static member Write(outFile : string, classifications : (ImageId * Classification) []) =
        let fs = File.OpenWrite(outFile)
        use sw = new StreamWriter(fs) 
        sw.WriteLine "ImageId,Label"
        classifications |> Array.iter (fun (i,c) -> sw.WriteLine(sprintf "%d,%d" i c))

/// l^2 distance 
let l2 : Distance =
    fun x y ->
        let xp = x.Pixels
        let yp = y.Pixels
        let mutable acc = 0uL
        for i = 0 to pixelLength - 1 do
            acc <- acc + uint64 (pown (xp.[i] - yp.[i]) 2)
        acc

/// single-threaded, stream-based k-nearest neighbour classifier
let knn (d : Distance) (k : int) : Classifier =
    fun (training : TrainingImage []) (img : Image) ->
        training
        |> Stream.ofArray
        |> Stream.sortBy (fun ex -> d ex.Image img)
        |> Stream.take k
        |> Stream.map (fun ex -> ex.Classification)
        |> Stream.countBy id
        |> Stream.maxBy snd
        |> fst

/// local multicore classification
let classifyLocalMulticore (classifier : Classifier) (training : TrainingImage []) (images : Image []) =
    ParStream.ofArray images
    |> ParStream.map (fun img -> img.Id, classifier training img)
    |> ParStream.toArray

/// local multicore validation
let validateLocalMulticore (classifier : Classifier) (training : TrainingImage []) (validation : TrainingImage []) =
    ParStream.ofArray validation
    |> ParStream.map(fun tr -> tr.Classification, classifier training tr.Image)
    |> ParStream.map(fun (expected,prediction) -> if expected = prediction then 1. else 0.)
    |> ParStream.sum
    |> fun results -> results / float validation.Length

////////////////////
// Run the examples

let trainPath = __SOURCE_DIRECTORY__ + "/../../data/train.csv"
let testPath = __SOURCE_DIRECTORY__ + "/../../data/test.csv"

// parse data
let training = TrainingImage.Parse trainPath
let tests = Image.Parse testPath

let classifier = knn l2 10

#time

//// Performance (3.5Ghz Quad Core i7 CPU)
//// Real: 00:01:02.281, CPU: 00:07:51.481, GC gen0: 179, gen1: 82, gen2: 62
//validateLocalMulticore classifier training.[ .. 39999] training.[40000 ..]
//
//// Performance (3.5Ghz Quad Core i7 CPU)
//// Real: 00:15:30.855, CPU: 01:56:59.842, GC gen0: 2960, gen1: 2339, gen2: 1513
//classifyLocalMulticore classifier training tests

/// Validate training images using MBrace
let validateCloud (classifier : Classifier) (training : TrainingImage []) (validation : TrainingImage []) = cloud {
    let! successCount =
        CloudFlow.OfArray validation
        |> CloudFlow.filter (fun tI -> classifier training tI.Image = tI.Classification)
        |> CloudFlow.length

    return float successCount / float validation.Length
}

/// Clasify test images using MBrace
let classifyCloud (classifier : Classifier) (training : TrainingImage []) (images : Image []) =
    CloudFlow.OfArray images
    |> CloudFlow.map (fun img -> img.Id, classifier training img)
    |> CloudFlow.toArray


// 1. Validation operation
let validateProc = cloud { return! validateCloud classifier training.[0 .. 39999] training.[40000 ..] } |> cluster.CreateProcess

// 2. Send Classify job
let classifyProc = cloud { return! classifyCloud classifier training tests } |> cluster.CreateProcess

cluster.ShowWorkers()
cluster.ShowProcesses()

