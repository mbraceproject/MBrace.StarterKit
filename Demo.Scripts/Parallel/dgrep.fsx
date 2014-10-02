#load "../../packages/MBrace.Runtime.0.5.7-alpha/bootstrap.fsx" 

open Nessos.MBrace
open Nessos.MBrace.Lib
open Nessos.MBrace.Client

//  Distributed Grep
//
//  Implements a distributed Grep-like workflow that operates on collections of files.
//  A collection of Cloud sequences containing the mathing occurrences is returned.

open System.IO
open System.Text.RegularExpressions

#I "../../bin/"
#r "Demo.Lib.dll"
open Demo.Lib

/// grep a single file
[<Cloud>]
let grepSingle (pattern : string) (file : ICloudFile) = cloud {
    let name = file.Name
    let! text = CloudFile.ReadLines file
    let regex = new Regex(pattern)
    return!
        text 
        |> Seq.mapi (fun i l -> (i,l))
        |> Seq.filter (snd >> regex.IsMatch)
        |> Seq.map (fun (i,l) -> sprintf "%s(%d):\t%s" name i l)
        |> CloudSeq.New
    }

/// Grep multiple files
[<Cloud>]
let grep (files : ICloudFile []) (pattern : string) = cloud {
    // granularity: files to be split into chunks of 5 in each job sent to workers
    return! Combinators.chunkMap (grepSingle pattern) 5 files
}

let runtime = MBraceRuntime.InitLocal(totalNodes = 4)

// First create cloudseqs from the local files
// We run the computation using the RunLocal function.
let source = __SOURCE_DIRECTORY__ +  @"\..\..\data\Shakespeare\"
let files = Directory.GetFiles source |> Seq.toArray

// upload files to store
let client = runtime.GetStoreClient()
let cFiles = client.UploadFiles files

// look for sentences ending in -king.
let ps = runtime.CreateProcess <@ grep cFiles "king\." @>

ps.ShowInfo()

ps.AwaitResult()
|> Seq.concat
|> Seq.iter (printfn "%s")