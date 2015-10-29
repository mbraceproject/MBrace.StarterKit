(*** hide ***)
#load "ThespianCluster.fsx"
//#load "AzureCluster.fsx"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit AzureCluster.fsx to enter your connection strings.

open System
open System.IO
open MBrace.Core
open MBrace.Flow

// Initialize client object to an MBrace cluster
let cluster = Config.GetCluster() 

(**
# Using CloudDictionary

> This tutorial is from the [MBrace Starter Kit](https://github.com/mbraceproject/MBrace.StarterKit).

In this example you learn how to use distributed key/value storage using CloudDictionary.
 
First, create a cloud key/value store (a cloud dictionary): *) 
let dict =
    cloud {
        let! dict = CloudDictionary.New<int> ()
        return dict
    } |> cluster.Run

(** Next, add an entry to the key/value store: *)
cloud { dict.ForceAdd ("key0", 42) } |> cluster.Run

(** Next, check that the entry exists in the key/value store: *)
cloud { return dict.ContainsKey "key0" } |> cluster.Run

(** Next, lookup the entry in the key/value store: *)
cloud { return dict.TryFind "key0" } |> cluster.Run

(** Next, lookup a key which is not present in the key/value store: *)
cloud { return dict.TryFind "key-not-there" } |> cluster.Run

(** Next, perform contested, distributed updates from many cloud workers: *) 
let key = "contestedKey"
let contestTask = 
    [|1 .. 100|]
    |> CloudFlow.OfArray
    |> CloudFlow.iter(fun i -> dict.AddOrUpdate(key, function None -> i | Some v -> i + v) |> ignore)
    |> cluster.CreateProcess

contestTask.ShowInfo()

(** Next, verify result is correct: *) 
cloud { return dict.TryFind key } |> cluster.Run

(** 
## Summary

In this tutorial, you've learned how to use key-value stores (i.e. dictionaries) in cloud storage.
Continue with further samples to learn more about the MBrace programming model.  


> Note, you can use the above techniques from both scripts and compiled projects. To see the components referenced 
> by this script, see [MBrace.Thespian.fsx](MBrace.Thespian.html) or [MBrace.Azure.fsx](MBrace.Azure.html).
*)
