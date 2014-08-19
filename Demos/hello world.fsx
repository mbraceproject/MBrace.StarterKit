#load "../packages/MBrace.Runtime.0.5.0-alpha/bootstrap.fsx" 

open Nessos.MBrace
open Nessos.MBrace.Client

// a simple cloud expression

[<Cloud>]
let hello () =
    cloud {
        return "hello, world!"
    }

// create a local-only runtime
let runtime = MBrace.InitLocal 4

// upload & execute
runtime.Run <@ hello () @>

// non-blocking process creation
let proc = runtime.CreateProcess <@ hello () @>

proc

proc.AwaitResult()

// show information
runtime.ShowProcessInfo()