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
# Using Cloud Queues

In this tutorial you learn how to create and use cloud queues, which allow you to send messages between
cloud workflows.  The state of queues is kept in cloud storage.
 
First, create an cloud queue:
**) 
let queue = CloudQueue.New<string>() |> cluster.Run

(** Next, you send to the channel by scheduling a cloud process to do the send: *)
cloud { queue.Enqueue "hello" } |> cluster.Run

(** Next, you receive from the channel by scheduling a cloud process to do the receive: *)
let msg = cloud { return queue.Dequeue() } |> cluster.Run

(** Next, you start a cloud task to send 100 messages to the queue: *)
let sendTask = 
    cloud { for i in [ 0 .. 100 ] do 
                queue.Enqueue (sprintf "hello%d" i) }
     |> cluster.CreateProcess

sendTask.ShowInfo() 

(** Next, you start a cloud task to wait for the 100 messages: *)
let receiveTask = 
    cloud { let results = new ResizeArray<_>()
            for i in [ 0 .. 100 ] do 
               let msg = queue.Dequeue()
               results.Add msg
            return results.ToArray() }
     |> cluster.CreateProcess

receiveTask.ShowInfo() 

(** Next, you wait for the result of the receiving cloud task: *)
receiveTask.Result

(** 
## Using queues as inputs to reactive data parallel cloud flows

You now learn how to use cloud queues as inputs to a data parallel cloud flow.

*)


#load "lib/sieve.fsx"

(** First, you create a request queue and an output queue: *)
let requestQueue = CloudQueue.New<int>() |> cluster.Run
let outputQueue = CloudQueue.New<int64>() |> cluster.Run

(** Next, you create a data parallel cloud workflow with 4-way parallelism that reads from the request queue. The requests are integer messages indicating a number of prime nnumbers to compute. The outputs are the sum of the prime numbers. *)

let processingFlow = 
    CloudFlow.OfCloudQueue(requestQueue, 4)
    |> CloudFlow.map (fun msg -> Array.sum (Array.map int64 (Sieve.getPrimes msg)))
    |> CloudFlow.toCloudQueue outputQueue
    |> cluster.CreateProcess

(** This task will continue running until it is explicitly cancelled or the queues are deleted. Check on the task using the following: *)
processingFlow.ShowInfo() 

(** Next, you start a cloud task to send 100 different requests to the queue: *)
let requestTask = 
    cloud { for i in [ 1 .. 100 ] do 
                do requestQueue.Enqueue (i * 100000 % 787853) }
     |> cluster.CreateProcess

requestTask.ShowInfo() 

cluster.ShowProcesses()

(** Next, you run a cloud task to collect up to 10 results from the output queue.  You can run this multiple times to collect all the results. *)
let collectedResults = 
    cloud { return outputQueue.DequeueBatch 10 }
     |> cluster.Run

(** 
## Summary

In this tutorial, you've learned how to use queues in cloud storage and how to use them
as inputs to data parallel cloud workflows. Continue with further samples to learn more 
about the MBrace programming model.  


> Note, you can use the above techniques from both scripts and compiled projects. To see the components referenced 
> by this script, see [MBrace.Thespian.fsx](MBrace.Thespian.html) or [MBrace.Azure.fsx](MBrace.Azure.html).
*)
