(*** hide ***)
#load "ThespianCluster.fsx"
//#load "AzureCluster.fsx"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit credentials.fsx to enter your connection strings.


open System
open System.IO
open System.Net
open System.Text.RegularExpressions
open MBrace.Core
open MBrace.Flow

// Initialize client object to an MBrace cluster
let cluster = Config.GetCluster() 

(**
# Example: Training in the Cloud

This example demonstrates Norvig's Spelling Corrector (http://norvig.com/spell-correct.html).
It is a prototypical workflow for training and learning in the cloud - we use the cloud to extract statistical
information from a body of text, and the statistical summary is used locally in your client application.
 
## Part 1 - Extract Statistics in the Cloud 
*)
#load "lib/collections.fsx"

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

let downloadTask = download "http://norvig.com/big.txt" |> cluster.CreateProcess

downloadTask.ShowInfo()

let files = downloadTask.Result

(** Now, take a look at the sizes of the files. *) 
let fileSizesJob = 
    files
    |> Array.map (fun f -> CloudFile.GetSize f.Path)
    |> Cloud.Parallel 
    |> cluster.CreateProcess 

fileSizesJob.Status
fileSizesJob.ShowInfo()

let fileSizes = fileSizesJob.Result

(**
In the second step, use cloud data flow to perform a parallel word 
frequency count on the stored text. 
*) 

let regex = Regex("[a-zA-Z]+", RegexOptions.Compiled)
let wordCountJob = 
    files
    |> Array.map (fun f -> f.Path)
    |> CloudFlow.OfCloudFileByLine
    |> CloudFlow.collect (fun text -> regex.Matches(text) |> Seq.cast)
    |> CloudFlow.map (fun (m:Match) -> m.Value.ToLower()) 
    |> CloudFlow.countBy id 
    |> CloudFlow.toArray
    |> cluster.CreateProcess

wordCountJob.ShowInfo()

cluster.ShowProcesses()

let NWORDS = wordCountJob.Result |> Map.ofArray

(** 

## Part 2 - Use the Frequency Counts in our Application

In the final step, use the calculated frequency counts to compute suggested spelling corrections in your client.
At this point, you've finished using the cluster and no longer need it.  
We have the computed the frequency table, all the rest of this example is run locally.

The statistics could be saved to disk for use in an application. We will use
them directly in the client.
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

(** In this example, you've seen how cloud tasks can be used to 
extract statistical information returned to the client.
Continue with further samples to learn more about the
MBrace programming model.   *)
