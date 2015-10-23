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

#load "lib/utils.fsx"
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
    hashSet [  
        "about"; "above"; "along"; "also"; "although"; "aren't"; "because"; "been";
        "cannot"; "could"; "couldn't"; "didn't"; "does"; "doesn't"; "e.g.";
        "either"; "etc."; "even"; "ever"; "from"; "further"; "gets"; "hardly";
        "hasn't"; "having"; "hence"; "here"; "hereby"; "herein"; "hereof";
        "hereon"; "hereto"; "herewith"; "however"; "i.e."; "into"; "it's"; "more";
        "most"; "near"; "onto"; "other"; "over"; "really"; "said"; "same";
        "should"; "shouldn't"; "since"; "some"; "such"; "than"; "that"; "their";
        "them"; "then"; "there"; "thereby"; "therefore"; "therefrom"; "therein";
        "thereof"; "thereon"; "thereto"; "therewith"; "these"; "they"; "this";
        "those"; "through"; "thus"; "under"; "until"; "unto"; "upon"; "very";
        "wasn't"; "were"; "what"; "when"; "where"; "whereby"; "wherein"; "whether";
        "which"; "while"; "whom"; "whose"; "with"; "without"; "would"; "your";
        "have"; "thou"; "will"; "shall" ]

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
cluster.ShowProcesses()

wordCountProc.Result