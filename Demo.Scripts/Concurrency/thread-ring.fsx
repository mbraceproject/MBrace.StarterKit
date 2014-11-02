#load "../../packages/MBrace.Runtime.0.5.10-alpha/bootstrap.fsx"

open Nessos.MBrace
open Nessos.MBrace.Client

open Nessos.MBrace.Lib
open Nessos.MBrace.Lib.Concurrency

(* This is a demonstration of the Channel type defined in the MBrace.Lib.Concurrency
 * namespace. We create some workers that communicate via channels and pass a token.
 * This demo is similar to the thread-ring benchmark.
 *)

// A message to be exchanged via the channels. Either a token, or a Halt command.
type Message = 
    | Token of int
    | Halt

// Each worker reads from its channel, decreases the token and posts the
// new token to the next worker.
[<Cloud>]
let rec worker (id : int) (n : int) (channels : Channel<Message> []) =
    cloud {
        let! msg = Channel.read channels.[id]

        let next = channels.[(id + 1) % n]

        match msg with
        | Halt ->
            do! Cloud.Logf "worker %d : halt" id
            do! Channel.write next Halt
        | Token t ->
            do! Cloud.Logf "worker %d : got token %d" id t
            if t > 0 then
                do! Channel.write next (Token (t-1))
            else
                do! Channel.write next Halt
            return! worker id n channels
    }

// Create the n channels and workers
// and the init computation that starts posts the first message
[<Cloud>]
let boot (n : int) (t : int) = 
    cloud {
        let! channels = Array.init n (fun _ -> Channel.newEmpty)
                        |> Cloud.Parallel
        let token = Token t
        let workers = Array.init n (fun id -> worker id n channels)
        let init = [| cloud { do! Channel.write channels.[0] token } |]
        let run = Array.append workers init

        do! Cloud.Parallel run
            |> Cloud.Ignore
    }

let runtime = MBrace.InitLocal(totalNodes = 4)

let n = 7          // number of 'actors'
let token = 1000   // initial token value, number of hops

let proc = runtime.CreateProcess <@ boot n token @>

runtime.ShowProcessInfo()
proc.AwaitResult()
proc.ShowLogs()