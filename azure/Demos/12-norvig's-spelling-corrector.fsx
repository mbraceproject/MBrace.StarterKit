#load "credentials.fsx"
#load "lib/collections.fsx"

open System
open System.IO
open System.Net
open System.Text.RegularExpressions
open MBrace
open MBrace.Azure
open MBrace.Azure.Client
open MBrace.Azure.Runtime
open MBrace.Streams
open MBrace.Workflows
open Nessos.Streams

(**
 This tutorial demonstrates a word count example via Norvig's Spelling Corrector (http://norvig.com/spell-correct.html)
 
 Before running, edit credentials.fsx to enter your connection strings.
**)

// First connect to the cluster
let cluster = Runtime.GetHandle(config)



/// helper : write all text to provided stream
let write (text: string) (stream: Stream) = async { 
    use writer = new StreamWriter(stream)
    writer.Write(text)
}

/// Step 1: download text file from source, 
/// saving it to blob storage chunked into smaller files of 10000 lines each
let download (uri: string) = cloud {
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
                let! file = CloudFile.WriteAllLines(lines, path = sprintf "text/%d.txt" index) 
                return file })
        |> Local.Parallel
    return files
}

let downloadJob = download "http://norvig.com/big.txt" |> cluster.CreateProcess

downloadJob.ShowInfo()

let files = downloadJob.AwaitResult()

let fileSizesJob = 
    files
    |> DivideAndConquer.map CloudFile.GetSize
    |> cluster.CreateProcess 

fileSizesJob.Completed
fileSizesJob.ShowInfo()

let fileSizes = fileSizesJob.AwaitResult()

// Step 2. Use MBrace.Streams to perform a parallel word 
// frequency count on the stored text
let regex = Regex("[a-zA-Z]+", RegexOptions.Compiled)
let wordCountJob = 
    files
    |> CloudStream.ofCloudFiles (fun s -> async { let sr = new StreamReader(s) in return sr.ReadToEnd() })
    |> CloudStream.collect (fun text -> regex.Matches(text) |> Seq.cast |> Stream.ofSeq)
    |> CloudStream.map (fun (m:Match) -> m.Value.ToLower()) 
    |> CloudStream.countBy id 
    |> CloudStream.toArray
    |> cluster.CreateProcess

wordCountJob.ShowInfo()

cluster.ShowProcesses()


// Step 3. Use calculated frequency counts to compute suggested spelling corrections 
let NWORDS = wordCountJob.AwaitResult() |> Map.ofArray

let isKnown word = NWORDS.ContainsKey word 

/// Compute the 1-character edits of the word
let edits1 (word: string) = 
    let splits = [for i in 0 .. word.Length do yield (word.[0..i-1], word.[i..])]
    let deletes = [for a, b in splits do if b <> "" then yield a + b.[1..]]
    let transposes = [for a, b in splits do if b.Length > 1 then yield a + string b.[1] + string b.[0] + b.[2..]]
    let replaces = [for a, b in splits do for c in 'a'..'z' do if b <> "" then yield a + string c + b.[1..]]
    let inserts = [for a, b in splits do for c in 'a'..'z' do yield a + string c + b]
    deletes @ transposes @ replaces @ inserts |> Set.ofList

edits1 "speling"
edits1 "pgantom"

/// Compute the 1-character edits of the word which are actually words
let knownEdits1 word = 
    let result = [for w in edits1 word do if Map.containsKey w NWORDS then yield w] |> Set.ofList
    if result.IsEmpty then None else Some result 

knownEdits1 "fantom"
knownEdits1 "pgantom"

/// Compute the 2-character edits of the word which are actually words
let knownEdits2 word = 
    let result = [for e1 in edits1 word do for e2 in edits1 e1 do if Map.containsKey e2 NWORDS then yield e2] |> Set.ofList
    if result.IsEmpty then None else Some result 

knownEdits2 "pgantom"
knownEdits2 "quyck"


/// Find the best correction for a word, preferring 0-edit, over 1-edit, over 
/// 2-edit, and sorting by frequency.
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

