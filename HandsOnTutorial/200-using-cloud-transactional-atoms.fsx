(*** hide ***)
#load "ThespianCluster.fsx"
#load "AzureCluster.fsx"

open System
open System.IO
open MBrace.Core
open MBrace.Azure
open MBrace.Flow

// Initialize client object to an MBrace cluster
let cluster = Config.GetCluster() 

(**
 This tutorial illustrates creating and using cloud atoms, which allow you to store data transactionally
 in cloud storage.
 
 Before running, edit credentials.fsx to enter your connection strings.
**)


/// Create an anoymous cloud atom with an initial value
let atom = CloudAtom.New(100) |> cluster.RunOnCloud

// Check the unique ID of the atom
atom.Id

// Get the value of the atom.
let atomValue = atom |> CloudAtom.Read |> cluster.RunOnCloud

// Transactionally update the value of the atom and output a result
let atomUpdateResult = CloudAtom.Transact (atom, fun x -> string x,x*x) |> cluster.RunOnCloud

// Have all workers atomically increment the counter in parallel
cloud {
    let! clusterSize = Cloud.GetWorkerCount()
    do!
        // Start a whole lot of updaters in parallel
        [ for i in 1 .. clusterSize * 2 -> 
             cloud { return! CloudAtom.Update (atom, fun i -> i + 1) } ]
        |> Cloud.Parallel
        |> Cloud.Ignore

    return! CloudAtom.Read atom
} |> cluster.RunOnCloud

// Delete the cloud atom
CloudAtom.Delete atom  |> cluster.RunOnCloud

cluster.ShowCloudTaskInfo()