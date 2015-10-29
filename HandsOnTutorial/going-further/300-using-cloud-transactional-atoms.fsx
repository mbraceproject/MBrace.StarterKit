(*** hide ***)
#load "../ThespianCluster.fsx"
//#load "../AzureCluster.fsx"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit AzureCluster.fsx to enter your connection strings.

open System
open System.IO
open MBrace.Core
open MBrace.Flow

// Initialize client object to an MBrace cluster
let cluster = Config.GetCluster() 

(**
 This tutorial illustrates creating and using cloud atoms, which allow you to store data transactionally
 in cloud storage.
 
**)


(** Create an anoymous cloud atom with an initial value: *)
let atom = CloudAtom.New(100) |> cluster.Run

(** Check the unique ID of the atom: *)
atom.Id

(** Get the value of the atom: *)
let atomValue = cloud { return atom.Value } |> cluster.Run

(** Transactionally update the value of the atom and output a result: *)
let atomUpdateResult = cloud { return atom.Transact(fun x -> string x,x*x) } |> cluster.Run

(** Have all workers atomically increment the counter in parallel *)
cloud {
    let! clusterSize = Cloud.GetWorkerCount()
    // Start a whole lot of updaters in parallel
    let! _ = Cloud.Parallel [ for i in 1 .. clusterSize * 2 -> cloud { atom.Update(fun i -> i + 1) } ]
    return atom.Value
} |> cluster.Run

(** Delete the cloud atom *)
atom.Dispose() |> Async.RunSynchronously

cluster.ShowProcesses()


(**
In this tutorial, you've learned how to use cloud transactional atoms.
Continue with further samples to learn more about the
MBrace programming model.   


> Note, you can use the above techniques from both scripts and compiled projects. To see the components referenced 
> by this script, see [MBrace.Thespian.fsx](MBrace.Thespian.html) or [MBrace.Azure.fsx](MBrace.Azure.html).
*)


