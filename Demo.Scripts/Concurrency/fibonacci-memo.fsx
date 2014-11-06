#load "../../packages/MBrace.Runtime.0.5.13-alpha/bootstrap.fsx" 

open Nessos.MBrace
open Nessos.MBrace.Client
open Nessos.MBrace.Lib

// A simple recursive and parallel Fibonacci implementation.
// This function is higher order and uses the f argument
// to memoize the Fibonacci values.
[<Cloud>]
let rec fib f n = 
    cloud { 
        if n < 1 then return 1I else
        let! (f1,f2) = f (n-1) <||> f (n-2) 
        return f1 + f2 
    }

// Use the memoize function from MBrace.Lib
// The first argument is the container/folder 
// in the store, the second one is a function
// that maps Fibonacci values to a filename
// and the third argument is the function to memoize.
[<Cloud>]
let rec fibs () = 
    Memoization.memoize 
        "fibsCache"
        string
        (fun n -> fib (fibs ()) n)

let runtime = MBrace.InitLocal(totalNodes = 4)

// Calculate and memoize the first 22 Fibonacci numbers.
let proc = runtime.CreateProcess <@ cloud { return! fibs () 22 } @>
proc.AwaitResult()
proc.ShowInfo()

// This one should execute almost instantly.
let proc' = runtime.CreateProcess <@ cloud { return! fibs () 23 } @>
proc'.AwaitResult()
proc'.ShowInfo()
