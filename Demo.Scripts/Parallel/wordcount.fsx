#load "../../packages/MBrace.Runtime.0.5.6-alpha/bootstrap.fsx"

open Nessos.MBrace
open Nessos.MBrace.Client

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

open System
open System.IO
open System.Text.RegularExpressions

/// words ignored by wordcount
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

let splitWords =
    let regex = new Regex(@"[\W]+", RegexOptions.Compiled)
    fun word -> regex.Split(word)

/// map function: reads file from given path and computes its wordcount
[<Cloud>]
let mapF (path : string) =
    cloud {
        let text = System.IO.File.ReadAllText(path)
        let words = splitWords text
        return 
            words
            |> Seq.map (fun word -> word.ToLower())
            |> Seq.map (fun t -> t.Trim())
            |> Seq.filter (fun word -> Seq.length word > 3 && not <| noiseWords.Contains(word) )
            |> Seq.groupBy id
            |> Seq.map (fun (key, values) -> (key, values |> Seq.length))
            |> Seq.toArray
    }

/// reduce function : combines two wordcount frequencies.
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


let runtime = MBrace.InitLocal(totalNodes = 4)

// fetch files from the data source
let fileSource = Path.Combine(__SOURCE_DIRECTORY__, @"..\..\data\Shakespeare")
let works = Directory.EnumerateFiles fileSource |> List.ofSeq

// start a cloud process
let proc = runtime.CreateProcess <@ mapReduce mapF reduceF [||] works @>

proc.ShowInfo()
runtime.ShowProcessInfo()
let results = proc.AwaitResult()

results |> Seq.take 6 |> Chart.bar "wordcount" // visualise results