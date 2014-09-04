#load "../../packages/MBrace.Runtime.0.5.6-alpha/bootstrap.fsx" 

open Nessos.MBrace
open Nessos.MBrace.Client
open Nessos.MBrace.Store

//
//  The following script demonstrates how an MBrace runtime can
//  be initialized given a collection of MBrace nodes
//

// a distributed runtime requires a shared store provider
// place your UNC/local path here
let store = FileSystemStore.Create "enter a filesystem path"

// sets the default store provider for the client
MBraceSettings.DefaultStore <- store

// spawn a pair of local nodes
let localNodes = MBraceNode.SpawnMultiple(nodeCount = 2, store = store)

// connect to remote mbrace nodes
let remoteNodes = 
    [
        "mbrace://host:port"
        "mbrace://host:port"
    ] |> List.map MBraceNode.Connect

// ping the nodes
remoteNodes |> List.map (fun n -> n.Ping())

// boot an mbrace runtime
let runtime = MBrace.Boot(remoteNodes @ localNodes, store = MBraceSettings.DefaultStore)

// show runtime information
runtime.ShowInfo(true)

// test the runtime
[<Cloud>]
let testComputation () =
    cloud {
        let! n = Cloud.GetWorkerCount ()
        return!
            [| 1 .. 10 * n |]
            |> Array.map (fun n -> cloud { return n * n })
            |> Cloud.Parallel
    }

runtime.Run <@ testComputation () @>

// show runtime information
runtime.ShowProcessInfo()