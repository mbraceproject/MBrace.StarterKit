// BEGIN PREAMBLE -- do not evaluate, for intellisense only
#r "Nessos.MBrace.Utils"
#r "Nessos.MBrace.Actors"
#r "Nessos.MBrace.Base"
#r "Nessos.MBrace.Store"
#r "Nessos.MBrace.Client"

open Nessos.MBrace.Client
// END PREAMBLE

// a distributed runtime requires a shared store provider
// place your UNC/local path here
let storeProvider = FileSystem "enter a filesystem path"

// sets the default store provider for the client
MBraceSettings.StoreProvider <- storeProvider

// spawn a pair of local nodes
let localNodes = MBraceNode.SpawnMultiple(2)

// connect to remote mbrace nodes
let remoteNodes = 
    [
        "mbrace://host:port"
        "mbrace://host:port"
    ] |> List.map (fun u -> MBraceNode u)

// ping the nodes
remoteNodes |> List.map (fun n -> n.Ping())

// boot an mbrace runtime
let runtime = MBrace.Boot(remoteNodes @ localNodes)

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