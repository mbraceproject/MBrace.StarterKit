(*** hide ***)
#load "../ThespianCluster.fsx"
//#load "../AzureCluster.fsx"

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

# Example: Cloud-distributed WordCount

> This example is from the [MBrace Starter Kit](https://github.com/mbraceproject/MBrace.StarterKit).


This example implements the classic word count example commonly associated with distributed Map/Reduce frameworks.
We use CloudFlow for the implementation and [textfiles.com](http://www.textfiles.com) as our data source.

First, some basic type definitions:

*)

#load "../lib/utils.fsx"
#load "../lib/textfiles.fsx"

type WordFrequency = string * int64
type WordCount = WordFrequency []

// Regex word tokenizer
let wordRegex = new Regex(@"[\W]+", RegexOptions.Compiled)
let splitToWords (line : string) = wordRegex.Split line

/// normalize word
let normalize (word : string) = word.Trim().ToLower()

(** Now, define the words to ignore in the word count: *)

/// The words ignored by wordcount
let noiseWords =
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

(**

We are now ready to define our distributed workflows.
First, we create a distributed download workflow that caches the contents of supplied urls across the cluster.
This returns a PersistedCloudFlow type that can be readily used for consumption by future flow queries.

*)

/// Downloads and caches text files across the cluster
let downloadAndCacheTextFiles (urls : seq<string>) : Cloud<PersistedCloudFlow<string>> =
    CloudFlow.OfHttpFileByLine urls
    |> CloudFlow.persist StorageLevel.Memory

(**

The wordcount function can now be defined:

*)

/// Computes the word count using the input cloud flow
let computeWordCount (cutoff : int) (lines : CloudFlow<string>) : Cloud<WordCount> =
    lines
    |> CloudFlow.collect splitToWords
    |> CloudFlow.map normalize
    |> CloudFlow.filter (not << isNoiseWord)
    |> CloudFlow.countBy id
    |> CloudFlow.sortBy (fun (_,c) -> -c) cutoff
    |> CloudFlow.toArray


(**

## Test the wordcount sample using textfiles.com


Step 1. Determine URIs to data inputs from textfiles.com
*)


let files = TextFiles.crawlForTextFiles() // get text file data from textfiles.com
let testedFiles = files // |> Seq.take 50 // uncomment to use a smaller dataset

(** Step 2. Download URIs to across cluster and load in memory *)
let downloadTask = 
    downloadAndCacheTextFiles testedFiles
    |> cluster.CreateProcess

cluster.ShowWorkers()
cluster.ShowProcesses()

let persistedFlow = downloadTask.Result // get PersistedCloudFlow

(** Step 3. Perform wordcount on downloaded data *)
let wordCountTask = 
    computeWordCount 100 persistedFlow 
    |> cluster.CreateProcess

(** Check progress: *)

cluster.ShowWorkers()
cluster.ShowProcesses()

(** Wait for the results: *)
wordCountTask.Result

(**
In this tutorial, you've learned how to perform a scalable textual analysis task using MBrace.
Continue with further samples to learn more about the
MBrace programming model.   


> Note, you can use the above techniques from both scripts and compiled projects. To see the components referenced 
> by this script, see [MBrace.Thespian.fsx](MBrace.Thespian.html) or [MBrace.Azure.fsx](MBrace.Azure.html).
*)


