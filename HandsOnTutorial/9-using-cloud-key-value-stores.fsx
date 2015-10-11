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

In this example you learn how to use distributed key/value storage using CloudDictionary.
 
First, create a cloud key/value store (a cloud dictionary): *) 
let dict =
    cloud {
        let! dict = CloudDictionary.New<int> ()
        return dict
    } |> cluster.Run

(** Next, add an entry to the key/value store: *)
CloudDictionary.Add "key0" 42 dict |> cluster.Run

(** Next, check that the entry exists in the key/value store: *)
CloudDictionary.ContainsKey "key0" dict |> cluster.Run

(** Next, lookup the entry in the key/value store: *)
CloudDictionary.TryFind "key0" dict |> cluster.Run

(** Next, lookup a key which is not present in the key/value store: *)
CloudDictionary.TryFind "key-not-there" dict |> cluster.Run

(** Next, perform contested, distributed updates from many cloud workers: *) 
let key = "contestedKey"
let contestTask = 
    [|1 .. 100|]
    |> CloudFlow.OfArray
    |> CloudFlow.iterLocal(fun i -> CloudDictionary.AddOrUpdate key (function None -> i | Some v -> i + v) dict |> Local.Ignore)
    |> cluster.CreateProcess

contestTask.ShowInfo()

(** Next, verify result is correct: *) 
CloudDictionary.TryFind key dict |> cluster.Run

(** 
## Summary

In this tutorial, you've learned how to use key-value stores (i.e. dictionaries) in cloud storage.
Continue with further samples to learn more about the MBrace programming model.  


> Note, you can use the above techniques from both scripts and compiled projects. To see the components referenced 
> by this script, see [MBrace.Thespian.fsx](MBrace.Thespian.html) or [MBrace.Azure.fsx](MBrace.Azure.html).
*)
