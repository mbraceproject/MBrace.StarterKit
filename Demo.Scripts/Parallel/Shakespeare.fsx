#load "../../packages/MBrace.Runtime.0.5.7-alpha/bootstrap.fsx"

open Nessos.MBrace
open Nessos.MBrace.Client
open Nessos.MBrace.Lib

//  MapReduce example
//
//  Provides a simplistic divide-and-conquer distributed MapReduce implementation 
//  using cloud workflows and the binary parallel decomposition operator (<||>).
//  This implementation is relatively naive since:
//
//      * Data is captured in closures and passed around continually between workers.
//      * Cluster size and multicore capacity of worker nodes not taken into consideration.
//  
//  For improved MapReduce implementations, please refer to the MBrace.Lib assembly.
//


#I "../../bin/"
#r "Demo.Lib.dll"
open Demo.Lib

[<Cloud>]
let rec mapReduce (mapF: 'T -> Cloud<'R>) 
                    (reduceF: 'R -> 'R -> Cloud<'R>)
                    (id : 'R) (input: 'T list) =         
    cloud {
        match input with
        | [] -> return id
        | [value] -> return! mapF value
        | _ ->
            let left, right = List.split input
            let! r1, r2 = 
                (mapReduce mapF reduceF id left)
                    <||> 
                (mapReduce mapF reduceF id right)
            return! reduceF r1 r2
    }



//
//  Example : wordcount on the works of Shakespeare.
//

open System.IO

#load "wordcount.fsx"
open Wordcount

/// map function: reads a cloud file and computes its wordcount
[<Cloud>]
let mapF (file : ICloudFile) = cloud {
    let! text = CloudFile.ReadAllText file
    return WordCount.compute text
}

/// reduce function: combines two frequency counts into one
[<Cloud>]
let reduceF (wc : WordCount) (wc' : WordCount) = cloud { return WordCount.combine wc wc' }

let runtime = MBrace.InitLocal(totalNodes = 4)

// fetch files from the data source
let fileSource = Path.Combine(__SOURCE_DIRECTORY__, @"..\..\data\Shakespeare")
let files = Directory.EnumerateFiles fileSource |> Array.ofSeq

// upload cloud files to runtime store
let client = runtime.GetStoreClient()
let cloudFiles = client.UploadFiles files |> List.ofArray

// start a cloud process
let proc = runtime.CreateProcess <@ mapReduce mapF reduceF [||] cloudFiles @>

proc.ShowInfo()
runtime.ShowProcessInfo()
let results = proc.AwaitResult()

results |> Seq.take 6 |> Chart.column "wordcount" // visualise results