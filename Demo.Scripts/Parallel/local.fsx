#load "../../packages/MBrace.Runtime.0.5.4-alpha/bootstrap.fsx" 

open Nessos.MBrace
open Nessos.MBrace.Client

// This script file is a demonstration of the async-cloud computation
// mixing.

// Create a runtime for testing.
let runtime = MBrace.InitLocal(totalNodes = 4)

// Test the Cloud.Sleep function.
// Write a (cloud) log 10 times with 1 sec interval.
[<Cloud>]
let write () = 
    cloud {
        for i in [|1..10|] do
            do! Cloud.Logf "Iteration %d" i
            do! Cloud.Sleep 1000
    }

let ps1 = runtime.CreateProcess <@ write () @>
ps1.AwaitResult()

// Fetch the user logs from the store, notice the timestamp.
ps1.ShowLogs()


// We can also execute a Cloud computation locally using 
// the local/Cloud.ToLocal combinator.
[<Cloud>]
let localParallel () =
    cloud {
        // get the square of some numbers
        let exprs = [| for i in [|1..100|] do yield cloud { return i * i } |]

        // this will be done in parallel distributed.
        let! sq1 = Cloud.Parallel exprs

        // this expr will be executed in a node, in parallel using async.
        let! sq2 = exprs |> Cloud.Parallel |> Cloud.ToLocal

        return (Seq.sum sq1, Seq.sum sq2)
    }


runtime.Run <@ localParallel () @>