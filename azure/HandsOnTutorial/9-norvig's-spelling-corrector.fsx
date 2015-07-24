(*** hide ***)
#load "credentials.fsx"
#load "lib/collections.fsx"


(**
# Training Machine Learning in the Cloud

 This tutorial demonstrates a word count example via Norvig's Spelling Corrector (http://norvig.com/spell-correct.html).
 It is a prototypical workflow for training machine learning in the cloud, and extracting the results
 to use locally in your client.
 
 Before running, edit credentials.fsx to enter your connection strings.
**)

open System
open System.IO
open System.Net
open System.Text.RegularExpressions
open MBrace.Core
open MBrace.Store
open MBrace.Azure
open MBrace.Azure.Client
open MBrace.Flow

(** First you connect to the cluster: *)
let cluster = Runtime.GetHandle(config)


(**
Step 1: download text file from source, 
saving it to blob storage chunked into smaller files of 10000 lines each. 
*) 
let download (uri: string) = 
    cloud {
        let webClient = new WebClient()
        do! Cloud.Log "Begin file download" 
        let! text = webClient.AsyncDownloadString(Uri(uri)) |> Cloud.OfAsync 
        do! Cloud.Log "file downloaded" 
        // Partition the big text into smaller files 
        let! files = 
            text.Split('\n')
            |> Array.chunkBySize 10000
            |> Array.mapi (fun index lines -> 
                 local { 
                    do! CloudFile.Delete(path = sprintf "text/%d.txt" index) 
                    let! file = CloudFile.WriteAllLines(path = sprintf "text/%d.txt" index, lines = lines) 
                    return file })
            |> Local.Parallel
        return files
    }

let downloadJob = download "http://norvig.com/big.txt" |> cluster.CreateProcess

downloadJob.ShowInfo()

let files = downloadJob.AwaitResult()

(** Now, take a look at the sizes of the files. *) 
let fileSizesJob = 
    files
    |> Array.map (fun f -> CloudFile.GetSize f.Path)
    |> Cloud.Parallel 
    |> cluster.CreateProcess 

fileSizesJob.Completed
fileSizesJob.ShowInfo()

let fileSizes = fileSizesJob.AwaitResult()

(**
In the second step, use cloud data flow to perform a parallel word 
frequency count on the stored text. 
*) 

let regex = Regex("[a-zA-Z]+", RegexOptions.Compiled)
let wordCountJob = 
    files
    |> Array.map (fun f -> f.Path)
    |> CloudFlow.OfCloudFilesByLine
    |> CloudFlow.collect (fun text -> regex.Matches(text) |> Seq.cast)
    |> CloudFlow.map (fun (m:Match) -> m.Value.ToLower()) 
    |> CloudFlow.countBy id 
    |> CloudFlow.toArray
    |> cluster.CreateProcess

wordCountJob.ShowInfo()

cluster.ShowProcesses()

let NWORDS = wordCountJob.AwaitResult() |> Map.ofArray

(** In the final step, use the calculated frequency counts to compute suggested spelling corrections in your client.
At this point, you've finished using the cluster and no longer need it.  
We have the commputed the frequency table, all the rest of this example is run locally.
*) 


let isKnown word = NWORDS.ContainsKey word 

(** Compute the 1-character edits of the word: *) 
let edits1 (word: string) = 
    let splits = [for i in 0 .. word.Length do yield (word.[0..i-1], word.[i..])]
    let deletes = [for a, b in splits do if b <> "" then yield a + b.[1..]]
    let transposes = [for a, b in splits do if b.Length > 1 then yield a + string b.[1] + string b.[0] + b.[2..]]
    let replaces = [for a, b in splits do for c in 'a'..'z' do if b <> "" then yield a + string c + b.[1..]]
    let inserts = [for a, b in splits do for c in 'a'..'z' do yield a + string c + b]
    deletes @ transposes @ replaces @ inserts |> Set.ofList

edits1 "speling"
edits1 "pgantom"

(** Compute the 1-character edits of the word which are actually words *) 
let knownEdits1 word = 
    let result = [for w in edits1 word do if Map.containsKey w NWORDS then yield w] |> Set.ofList
    if result.IsEmpty then None else Some result 

knownEdits1 "fantom"
knownEdits1 "pgantom"

(** Compute the 2-character edits of the word which are actually words *) 
let knownEdits2 word = 
    let result = [for e1 in edits1 word do for e2 in edits1 e1 do if Map.containsKey e2 NWORDS then yield e2] |> Set.ofList
    if result.IsEmpty then None else Some result 

knownEdits2 "pgantom"
knownEdits2 "quyck"


(** Find the best correction for a word, preferring 0-edit, over 1-edit, over 2-edit, and sorting by frequency. *) 
let findBestCorrection (word: string) = 
    let words = 
        if isKnown word then Set.ofList [word] 
        else 
            match knownEdits1 word with
            | Some words -> words
            | None ->
            match knownEdits2 word with
            | Some words -> words
            | None -> Set.ofList [word]

    words |> Seq.sortBy (fun w -> -NWORDS.[w]) |> Seq.head

// Examples
findBestCorrection "speling"
findBestCorrection "korrecter"
findBestCorrection "fantom"
findBestCorrection "pgantom"

