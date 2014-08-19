// Assembly references for intellisense purposes only
#r "Nessos.MBrace"
#r "Nessos.MBrace.Utils"
#r "Nessos.MBrace.Common"
#r "Nessos.MBrace.Actors"
#r "Nessos.MBrace.Store"
#r "Nessos.MBrace.Client"

open Nessos.MBrace
open Nessos.MBrace.Client

open System.IO
open System.Text.RegularExpressions

// First create cloudseqs from the local files
// We run the computation using the RunLocal function.
let files_dir = __SOURCE_DIRECTORY__ +  @"\..\Shakespeare\"

let cseqs = 
    Directory.GetFiles(files_dir)
    |> Seq.take 2
    |> Seq.toArray
    |> Array.map (fun file ->
         cloud { return! CloudSeq.New(File.ReadLines(file)) })
    |> Array.map MBrace.RunLocal

// This function takes a seq<string> and 
// returns the lines matching the pattern (slightly modified),
// as well as the line number (starting from 0 of course ;-) )
// A CloudSeq is used to return the result.
[<Cloud>]
let grep (pattern : string) (text : seq<string>) = cloud {
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
let run (cseqs : #seq<string> []) (pattern : string) =
  cloud {
    return! cseqs |> Array.map (fun s -> 
                        cloud { return! grep pattern s })
                  |> Cloud.Parallel
  }


let runtime = MBraceRuntime.InitLocal 4

let ps = runtime.CreateProcess <@ run cseqs " king" @>

ps.ShowInfo()

ps.AwaitResult()
|> Seq.concat
|> Seq.iter (fun (i,l) -> printfn "%i\t%s" i l)
