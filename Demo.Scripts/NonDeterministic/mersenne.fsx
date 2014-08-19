#load "../../packages/MBrace.Runtime.0.5.0-alpha/bootstrap.fsx" 

open Nessos.MBrace
open Nessos.MBrace.Client

#r "../../bin/Demo.Lib.dll"

open System.Numerics
open Demo.Lib

//
//  Checking for Mersenne primes using {mbrace}
//  a Mersenne prime is a number of the form 2^p - 1 that is prime
//  the library defines a simple checker that uses the Lucas-Lehmer test

#time

// sequential Mersenne Prime Search
let tryFindMersenneSeq ts = Array.tryFind Primality.isMersennePrime ts

tryFindMersenneSeq [| 2500 .. 3500 |]

// a general-purpose non-deterministic search combinator for the cloud

[<Cloud>]
let tryFind (f : 'T -> bool) partitionSize (inputs : 'T []) =
    let searchLocal (inputs : 'T []) =
        inputs
        |> Array.map (fun t -> cloud { return if f t then Some t else None })
        |> Cloud.Choice
        |> local // local combinator restricts computation to local async semantics

    cloud {
        return!
            inputs
            |> Array.partition partitionSize
            |> Array.map searchLocal
            |> Cloud.Choice
    }

let runtime = MBrace.InitLocal 4

let proc = runtime.CreateProcess <@ tryFind Primality.isMersennePrime 100 [|2500 .. 3500|] @>

proc

proc.AwaitResult()