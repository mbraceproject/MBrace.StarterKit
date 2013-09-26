// BEGIN PREAMBLE -- do not evaluate, for intellisense only
#r "Nessos.MBrace.Utils"
#r "Nessos.MBrace.Actors"
#r "Nessos.MBrace.Base"
#r "Nessos.MBrace.Store"
#r "Nessos.MBrace.Client"

open Nessos.MBrace.Client
// END PREAMBLE

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