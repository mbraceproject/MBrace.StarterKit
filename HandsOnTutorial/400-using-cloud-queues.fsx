(*** hide ***)
#load "ThespianCluster.fsx"
#load "AzureCluster.fsx"

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
# Using Cloud Queues

This tutorial illustrates creating and using cloud channels, which allow you to send messages between
cloud workflows.
 
Before running, edit credentials.fsx to enter your connection strings.
*)

(** Create an anonymous cloud channel: *) 
let queue = CloudQueue.New<string>() |> cluster.RunOnCloud

(** Send to the channel by scheduling a cloud process to do the send: *)
CloudQueue.Enqueue (queue, "hello") |> cluster.RunOnCloud

(**  Receive from the channel by scheduling a cloud process to do the receive: *)
let msg = CloudQueue.Dequeue(queue) |> cluster.RunOnCloud

let sendTask = 
    cloud { for i in [ 0 .. 100 ] do 
                do! queue.Enqueue (sprintf "hello%d" i) }
     |> cluster.CreateCloudTask

sendTask.ShowInfo() 

(** Wait for the 100 messages: *)
let receiveTask = 
    cloud { let results = new ResizeArray<_>()
            for i in [ 0 .. 100 ] do 
               let! msg = CloudQueue.Dequeue(queue)
               results.Add msg
            return results.ToArray() }
     |> cluster.CreateCloudTask

receiveTask.ShowInfo() 
receiveTask.Result