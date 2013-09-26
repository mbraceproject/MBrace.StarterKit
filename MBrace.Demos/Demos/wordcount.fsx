// BEGIN PREAMBLE -- do not evaluate, for intellisense only
#r "Nessos.MBrace.Utils"
#r "Nessos.MBrace.Actors"
#r "Nessos.MBrace.Base"
#r "Nessos.MBrace.Store"
#r "Nessos.MBrace.Client"

open Nessos.MBrace.Client
// END PREAMBLE

#load "ExcelEnv.fsx"
open Microsoft.Office.Interop.Excel

#r "../Nessos.MBrace.Lib/bin/Debug/Nessos.MBrace.Lib.dll"
open Nessos.MBrace.Lib

// simple mapReduce implementation
[<Cloud>]
let rec mapReduce (mapF: 'T -> ICloud<'R>) 
                  (reduceF: 'R -> 'R -> ICloud<'R>) 
                  (identity: 'R) 
                  (input: 'T list) =
    cloud {
        match input with
        | [] -> return identity
        | [value] -> return! mapF value
        | _ ->
            let firstHalf, secondHalf = List.split input

            // (<||>) : Cloud<'a> -> Cloud<'b> -> Cloud<'a * 'b>
            //  mbrace's binary parallel decomposition operator
            let! first, second =
                (mapReduce mapF reduceF identity firstHalf)
                  <||> (mapReduce mapF reduceF identity secondHalf)

            return! reduceF first second
    }



//
//  mapReduce example : Shakespeare word count
//

open System
open System.IO


let fileSource = Path.Combine(__SOURCE_DIRECTORY__, @"..\Shakespeare")
let works = Directory.EnumerateFiles fileSource |> List.ofSeq

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
let mapF (path : string) =
    cloud {
        let text = System.IO.File.ReadAllText(path)
        let words = text.Split([|' '; '.'; ','|], StringSplitOptions.RemoveEmptyEntries)
        return 
            words
            |> Seq.map (fun word -> word.ToLower())
            |> Seq.map (fun t -> t.Trim())
            |> Seq.filter (fun word -> Seq.length word > 3 && not <| noiseWords.Contains(word) )
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

let proc = runtime.CreateProcess <@ mapReduce mapF reduceF [||] works @>

proc.ShowInfo()
proc.AwaitResult()
runtime.ShowProcessInfo()

// Excel visualization
let chart = Excel.newChart()
proc.AwaitResult() |> Seq.take 10 |> Excel.draw chart XlChartType.xl3DPie "WordCount" |> ignore
