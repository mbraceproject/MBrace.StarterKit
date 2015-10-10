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
# Using Cloud Queues

This tutorial illustrates creating and using cloud channels, which allow you to send messages between
cloud workflows.
 
*)

(** Create an anonymous cloud channel: *) 
let queue = CloudQueue.New<string>() |> cluster.Run

(** Send to the channel by scheduling a cloud process to do the send: *)
CloudQueue.Enqueue (queue, "hello") |> cluster.Run

(**  Receive from the channel by scheduling a cloud process to do the receive: *)
let msg = CloudQueue.Dequeue(queue) |> cluster.Run

let sendTask = 
    cloud { for i in [ 0 .. 100 ] do 
                do! queue.Enqueue (sprintf "hello%d" i) }
     |> cluster.CreateProcess

sendTask.ShowInfo() 

(** Wait for the 100 messages: *)
let receiveTask = 
    cloud { let results = new ResizeArray<_>()
            for i in [ 0 .. 100 ] do 
               let! msg = CloudQueue.Dequeue(queue)
               results.Add msg
            return results.ToArray() }
     |> cluster.CreateProcess

receiveTask.ShowInfo() 
receiveTask.Result