

#load "credentials.fsx"

open MBrace
open MBrace.Azure
open MBrace.Azure.Client
open MBrace.Azure.Runtime
open MBrace.Streams
open MBrace.Workflows
open Nessos.Streams

let cluster = Runtime.GetHandle(config)

let ps = 
 [let time = ref System.DateTime.Now.TimeOfDay
  for i in 0 .. 10000 ->
   let newTime = System.DateTime.Now.TimeOfDay
   printfn "starting %d, diff = %A" i (time.Value - newTime)
   time := newTime
   cloud { return System.DateTime.Now }
    |> cluster.CreateProcess ]

cluster.ShowProcesses()



