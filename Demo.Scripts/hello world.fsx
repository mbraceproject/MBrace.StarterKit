#load "../packages/MBrace.Runtime.0.5.6-alpha/bootstrap.fsx" 

open Nessos.MBrace
open Nessos.MBrace.Client

// a simple cloud expression

[<Cloud>]
let hello () =
    cloud {
        return "hello, world!"
    }

MBrace.RunLocal <@ hello () @> // local evaluation

// create a local-only runtime
let runtime = MBrace.InitLocal(totalNodes = 4)
//// connect to a booted runtime
//let runtime = MBrace.Connect "mbrace://host:port"

// upload & execute
runtime.Run <@ hello () @>

// non-blocking process creation
let proc = runtime.CreateProcess <@ hello () @>

proc

proc.AwaitResult()

// show information
runtime.ShowProcessInfo()