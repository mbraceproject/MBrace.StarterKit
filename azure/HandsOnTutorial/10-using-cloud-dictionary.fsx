#load "credentials.fsx"

open System
open System.IO
open MBrace.Core
open MBrace.Store
open MBrace.Azure
open MBrace.Azure.Client
open MBrace.Flow

(**
 This example distributed key/value storage using CloudDictionary
 
 Before running, edit credentials.fsx to enter your connection strings.
**)

// First connect to the cluster
let cluster = Runtime.GetHandle(config)

// create a cloud dictionary
let dict =
    cloud {
        let! dict = CloudDictionary.New<int> ()
        return dict
    } |> cluster.Run

// add an entry to the dictionary
cluster.StoreClient.Dictionary.Add "key0" 42 dict
cluster.StoreClient.Dictionary.ContainsKey "key0" dict
cluster.StoreClient.Dictionary.TryFind "key0" dict

// contested, distributed updates
let key = "contestedKey"
let contestJob = 
    [|1 .. 100|]
    |> CloudFlow.OfArray
    |> CloudFlow.iterLocal(fun i -> CloudDictionary.AddOrUpdate key (function None -> i | Some v -> i + v) dict |> Local.Ignore)
    |> cluster.CreateProcess

contestJob.ShowInfo()

// verify result is correct
cluster.StoreClient.Dictionary.TryFind key dict