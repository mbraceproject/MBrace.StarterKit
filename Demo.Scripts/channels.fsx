// BEGIN PREAMBLE -- do not evaluate, for intellisense only
#load "../packages/MBrace.Runtime.0.5.6-alpha/bootstrap.fsx" 

// END OF PREAMBLE

open Nessos.MBrace.Client

#r "../Nessos.MBrace.Lib/bin/Debug/Nessos.MBrace.Lib.dll"
open Nessos.MBrace.Lib
open Nessos.MBrace.Lib.Concurrency

type Message = Ping | Pong

[<Cloud>]
let rec actorA (channel : Channel<Message>) = cloud {
    let! msg = Channel.read channel
    match msg with
    | Ping -> do! Channel.write channel Pong
    | Pong -> ()
    return! actorA channel
}

[<Cloud>]
let rec actorB (channels : Channel<Message> list) = cloud {
    do!
        channels
        |> List.toArray
        |> Array.map (fun channel -> cloud {
            while true do
                do! Channel.write channel Ping
                let! msg = Channel.read channel
                match msg with
                | Ping -> ()
                | Pong -> () })
        |> Cloud.Parallel
        |> Cloud.Ignore
}




