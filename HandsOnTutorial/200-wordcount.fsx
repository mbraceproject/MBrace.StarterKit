(*** hide ***)
#load "ThespianCluster.fsx"
//#load "AzureCluster.fsx"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit AzureCluster.fsx to enter your connection strings.

open System
open System.Collections.Generic
open System.IO
open System.Text.RegularExpressions

open MBrace.Core
open MBrace.Flow

// Initialize client object to an MBrace cluster
let cluster = Config.GetCluster() 

(**

# Simple WordCount

*)

#load "lib/textfiles.fsx"

type WordFrequency = string * int64
type WordCount = WordFrequency []

let private wordRegex = new Regex(@"[\W]+", RegexOptions.Compiled)
/// Regex word tokenizer
let splitToWords (line : string) = wordRegex.Split line

/// normalize word
let normalize (word : string) = word.Trim().ToLower()

/// words ignored by wordcount
let private noiseWords = 
    [|
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
    |] |> fun w -> new HashSet<_>(w)

/// specifies whether word is noise
let isNoiseWord (word : string) = word.Length <= 3 || noiseWords.Contains word

/// Downloads and caches text files across the cluster
let downloadAndCacheTextFiles (urls : seq<string>) : Cloud<PersistedCloudFlow<string>> =
    CloudFlow.OfHttpFileByLine urls
    |> CloudFlow.persist StorageLevel.Memory

/// Computes the word count using the input cloud flow
let computeWordCount (cutoff : int) (words : CloudFlow<string>) : Cloud<WordCount> =
    words
    |> CloudFlow.collect splitToWords
    |> CloudFlow.map normalize
    |> CloudFlow.filter (not << isNoiseWord)
    |> CloudFlow.countBy id
    |> CloudFlow.sortBy (fun (_,c) -> -c) cutoff
    |> CloudFlow.toArray


////////////////////////////////////////////////
// Test the wordcount sample using textfiles.com


// Step 1. Determine URIs to data inputs from textfiles.com
let files = TextFiles.crawlForTextFiles() // get text file data from textfiles.com
let testedFiles = files // |> Seq.take 50 // uncomment if you want to use a smaller subset

// Step 2. Download URIs to memory of workers in cluster
let downloadProc = cluster.CreateProcess(downloadAndCacheTextFiles testedFiles) // download text and load to cluster

cluster.ShowWorkers()
cluster.ShowProcesses()

let persistedFlow = downloadProc.Result // get PersistedCloudFlow

// Step 3. Perform wordcount on downloaded data
let wordCountProc = cluster.CreateProcess(computeWordCount 100 persistedFlow) // perform the wordcount operation

cluster.ShowWorkers()
<<<<<<< Updated upstream
cluster.ShowProcesses()
=======

wordCountProc.ShowInfo()
wordCountProc.Result

cluster.Workers.Length

cluster.ShowProcesses()



open System
open System.IO

let files = Directory.EnumerateFiles("/Users/eirik/Desktop/textFiles.com") |> Seq.toArray

files |> Array.Parallel.map (fun f -> cluster.Store.CloudFileSystem.File.Upload(f, "/textfiles/" + Path.GetFileName f))
cluster.Store.CloudFileSystem.File.Upload(files, "/textfiles/")
let data = files |> Seq.map File.ReadAllLines |> Seq.toArray




#time
>>>>>>> Stashed changes

wordCountProc.Result