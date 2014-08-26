#load "../../packages/MBrace.Runtime.0.5.4-alpha/bootstrap.fsx"

open Nessos.MBrace
open Nessos.MBrace.Client

open Nessos.MBrace.Lib
open Nessos.MBrace.Lib.MapReduce

#r "../../bin/Demo.Lib.dll"
open Demo.Lib

open System
open System.IO

// An implementation of the mapReduce function (see wordcount example).
// Uses somewhat dynamic parallelism. It spawns a number of cloud computations proportional to the
// number of the workers, and then creates some async computations (proportional to the number of
// hardware threads on a machine). Finally the execution is sequential.

let noiseWords = 
    set [
        "a"; "about"; "above"; "all"; "along"; "also"; "although"; "am"; "an"; "any"; "are"; "aren't"; "as"; "at";
        "be"; "because"; "been"; "but"; "by"; "can"; "cannot"; "could"; "couldn't"; "did"; "didn't"; "do"; "does"; 
        "doesn't"; "e.g."; "either"; "etc"; "etc."; "even"; "ever";"for"; "from"; "further"; "get"; "gets"; "got"; 
        "had"; "hardly"; "has"; "hasn't"; "having"; "he"; "hence"; "her"; "here"; "hereby"; "herein"; "hereof"; 
        "hereon"; "hereto"; "herewith"; "him"; "his"; "how"; "however"; "I"; "i.e."; "if"; "into"; "it"; "it's"; "its";
        "me"; "more"; "most"; "mr"; "my"; "near"; "nor"; "now"; "of"; "onto"; "other"; "our"; "out"; "over"; "really"; 
        "said"; "same"; "she"; "should"; "shouldn't"; "since"; "so"; "some"; "such"; "than"; "that"; "the"; "their"; 
        "them"; "then"; "there"; "thereby"; "therefore"; "therefrom"; "therein"; "thereof"; "thereon"; "thereto"; 
        "therewith"; "these"; "they"; "this"; "those"; "through"; "thus"; "to"; "too"; "under"; "until"; "unto"; "upon";
        "us"; "very"; "viz"; "was"; "wasn't"; "we"; "were"; "what"; "when"; "where"; "whereby"; "wherein"; "whether";
        "which"; "while"; "who"; "whom"; "whose"; "why"; "with"; "without"; "would"; "you"; "your" ; "have"; "thou"; "will"; 
        "shall"
    ]

// map function

[<Cloud>]
let mapF (file : ICloudFile) =
    cloud {
        let! text = CloudFile.ReadAllText file
        let words = text.Split([|' '; '.'; ','|], StringSplitOptions.RemoveEmptyEntries)
        let wordRegex = new System.Text.RegularExpressions.Regex("^[a-zA-Z]*$")
        return 
            words
            |> Seq.map (fun word -> word.Trim().ToLower())
            |> Seq.filter (fun word -> Seq.length word > 3 && wordRegex.IsMatch word && not <| noiseWords.Contains word)
            |> Seq.groupBy id
            |> Seq.map (fun (key, values) -> (key, values |> Seq.length))
            |> Seq.toArray
    }

// reduce function

[<Cloud>]
let reduceF (left: (string * int) []) (right: (string * int) []) = 
    cloud {
        return 
            Seq.append left right 
            |> Seq.groupBy fst 
            |> Seq.map (fun (key, value) -> (key, value |> Seq.sumBy snd ))
            |> Seq.sortBy (fun (_,t) -> -t)
            |> Seq.toArray
    }

let runtime = MBrace.InitLocal 4

// data source is an array of local CloudFiles
let fileSource = Path.Combine(__SOURCE_DIRECTORY__, @"..\..\data\Wikipedia")
let files = Directory.GetFiles fileSource |> Seq.toArray

// upload local files to runtime
let client = runtime.GetStoreClient()
let cloudFiles = client.UploadFiles files

let proc = runtime.CreateProcess <@ Seq.mapReduce mapF reduceF (fun () -> cloud { return [||] }) cloudFiles @>
let result = proc.AwaitResult()