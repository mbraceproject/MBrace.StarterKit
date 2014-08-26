#load "../../packages/MBrace.Runtime.0.5.4-alpha/bootstrap.fsx" 

open Nessos.MBrace
open Nessos.MBrace.Lib
open Nessos.MBrace.Client

open System.IO
open System.Text.RegularExpressions

// This function takes a seq<string> and 
// returns the lines matching the pattern (slightly modified),
// as well as the line number (starting from 0 of course ;-) )
// A CloudSeq is used to return the result.
[<Cloud>]
let grep (pattern : string) (file : ICloudFile) = cloud {
    let! text = CloudFile.ReadLines file
    let is_match line = Regex.IsMatch(line, pattern)
    let highlight line = Regex.Replace(line, pattern, sprintf "*%s*" (pattern.ToUpper()))
    return!
        text |> Seq.mapi (fun i l -> i,l)
             |> Seq.filter (snd >> is_match)
             |> Seq.map (fun (i,l) -> i, (highlight l).Trim())
             |> CloudSeq.New
    }

// Orchestrate the cloud execution; simple map
[<Cloud>]
let run (files : ICloudFile []) (pattern : string) =
  cloud {
    return! 
        files
        |> Array.map (fun s -> cloud { return! grep pattern s })
        |> Cloud.Parallel
  }

let runtime = MBraceRuntime.InitLocal 4

// First create cloudseqs from the local files
// We run the computation using the RunLocal function.
let source = __SOURCE_DIRECTORY__ +  @"\..\..\data\Shakespeare\"
let files = 
    Directory.GetFiles source
    |> Seq.take 5
    |> Seq.toArray

// upload files to store
let client = runtime.GetStoreClient()
let cFiles = client.UploadFiles files

let ps = runtime.CreateProcess <@ run cFiles " king" @>

ps.ShowInfo()

ps.AwaitResult()
|> Seq.concat
|> Seq.iter (fun (i,l) -> printfn "%i\t%s" i l)