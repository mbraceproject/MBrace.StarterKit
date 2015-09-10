(*** hide ***)
#load "Thespian.fsx"
#load "Azure.fsx"

open System
open System.IO
open MBrace.Core
open MBrace.Azure
open MBrace.Flow

// Initialize client object to an MBrace cluster:
let cluster = 
//    getAzureClient() // comment out to use an MBrace.Azure cluster; don't forget to set the proper connection strings in Azure.fsx
    initThespianCluster(4) // use a local cluster based on MBrace.Thespian; configuration can be adjusted using Thespian.fsx

(**
# Using CloudDictionary

In this example you learn how to use distributed key/value storage using CloudDictionary.
 
Before running, edit credentials.fsx to enter your connection strings.
*)

(** Next, create a cloud dictionary: *) 
let dict =
    cloud {
        let! dict = CloudDictionary.New<int> ()
        return dict
    } |> cluster.RunOnCloud

(** Next, add an entry to the dictionary: *)
dict.Add("key0", 42) |> Async.RunSynchronously
dict.ContainsKey "key0" |> Async.RunSynchronously
dict.TryFind "key0" |> Async.RunSynchronously
dict.TryFind "key-not-there" |> Async.RunSynchronously

(** Next, perform contested, distributed updates: *) 
let key = "contestedKey"
let contestTask = 
    [|1 .. 100|]
    |> CloudFlow.OfArray
    |> CloudFlow.iterLocal(fun i -> CloudDictionary.AddOrUpdate key (function None -> i | Some v -> i + v) dict |> Local.Ignore)
    |> cluster.CreateCloudTask

contestTask.ShowInfo()

(** Next, verify result is correct: *) 
dict.TryFind key |> Async.RunSynchronously
