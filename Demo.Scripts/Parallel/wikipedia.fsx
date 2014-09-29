#load "../../packages/MBrace.Runtime.0.5.7-alpha/bootstrap.fsx"

open Nessos.MBrace
open Nessos.MBrace.Client

//  Wikipedia wordcount
//
//  Performs wordcount computation on a downloaded wikipedia dataset.
//  Makes use of CloudFiles and the Library MapReduce implementation.
//

open Nessos.MBrace.Lib
open Nessos.MBrace.Lib.MapReduce

open System.IO

#load "wordcount.fsx"
open Wordcount

/// map function: reads a CloudFile from given path and computes its wordcount
[<Cloud>]
let mapF (file : ICloudFile) = cloud {
    let! text = CloudFile.ReadAllText file
    return WordCount.compute text
}

/// reduce function : combines two wordcount frequencies.
[<Cloud>]
let reduceF (wc : WordCount) (wc' : WordCount) = cloud { return WordCount.reduce wc wc' }

let runtime = MBrace.InitLocal(totalNodes = 4)

// data source is an array of local CloudFiles
let fileSource = Path.Combine(__SOURCE_DIRECTORY__, @"..\..\data\Wikipedia")
let files = Directory.GetFiles fileSource |> Seq.toArray

// upload local files to runtime
let client = runtime.GetStoreClient()
let cloudFiles = client.UploadFiles files

let proc = runtime.CreateProcess <@ Seq.mapReduce mapF reduceF (fun () -> cloud { return [||] }) cloudFiles @>
let result = proc.AwaitResult()