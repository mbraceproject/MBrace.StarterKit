(*** hide ***)
#load "ThespianCluster.fsx"
//#load "AzureCluster.fsx"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit credentials.fsx to enter your connection strings.

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
dict.Add("key0", 42) |> Async.RunSynchronously

(** Next, check that the entry exists in the key/value store: *)
dict.ContainsKey "key0" |> Async.RunSynchronously

(** Next, lookup the entry in the key/value store: *)
dict.TryFind "key0" |> Async.RunSynchronously

(** Next, lookup a key which is not present in the key/value store: *)
dict.TryFind "key-not-there" |> Async.RunSynchronously

(** Next, perform contested, distributed updates from many cloud workers: *) 
let key = "contestedKey"
let contestTask = 
    [|1 .. 100|]
    |> CloudFlow.OfArray
    |> CloudFlow.iterLocal(fun i -> CloudDictionary.AddOrUpdate key (function None -> i | Some v -> i + v) dict |> Local.Ignore)
    |> cluster.CreateProcess

contestTask.ShowInfo()

(** Next, verify result is correct: *) 
dict.TryFind key |> Async.RunSynchronously
