#load "../../packages/MBrace.Runtime.0.5.4-alpha/bootstrap.fsx" 

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

// sequential Mersenne prime search
let tryFindMersenneSeq ts = Array.tryFind Primality.isMersennePrime ts

tryFindMersenneSeq [| 2500 .. 3500 |]

// MBrace Mersenne prime search

let runtime = MBrace.InitLocal 4

let proc = runtime.CreateProcess <@ Combinators.tryFind Primality.isMersennePrime 100 [|2500 .. 3500|] @>

proc

proc.AwaitResult()