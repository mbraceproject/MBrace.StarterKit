#load "../../packages/MBrace.Runtime.0.5.7-alpha/bootstrap.fsx" 

open Nessos.MBrace
open Nessos.MBrace.Client

//
//  The following script demonstrates how logging can take place in cloud processes
//

[<Cloud>]
let log i = cloud {
    do! Cloud.Log <| sprintf "this is cloud log entry #%d" i
    return ()
}

// run locally
MBrace.RunLocal(log 0, showLogs = true)

// run remotely
let runtime = MBrace.InitLocal(totalNodes = 3)

let proc = runtime.CreateProcess <@ log 0 @>
proc.ShowLogs()

// multiple sequential logs

[<Cloud>]
let logSequential () = cloud {
    for i in [1 .. 10] do
        do! log i
}

let proc' = runtime.CreateProcess <@ logSequential () @>

// multiple parallel log entries

[<Cloud>]
let logParallel () = cloud {
    let! _ = List.init 10 log |> Cloud.Parallel

    do! Cloud.Log "completed"

    return ()
}

let proc'' = runtime.CreateProcess <@ logParallel () @>

proc''.ShowLogs()


/// Cloud.Trace

let proc''' = runtime.CreateProcess <@ Cloud.Trace <| logParallel () @>

proc'''.ShowLogs()