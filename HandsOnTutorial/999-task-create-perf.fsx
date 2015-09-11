(*** hide ***)
#load "ThespianCluster.fsx"
//#load "AzureCluster.fsx"

open System
open System.IO
open MBrace.Core
open MBrace.Flow

// Initialize client object to an MBrace cluster
let cluster = Config.GetCluster() 

let ps = 
 [let time = ref System.DateTime.Now.TimeOfDay
  for i in 0 .. 10000 ->
   let newTime = System.DateTime.Now.TimeOfDay
   printfn "starting %d, diff = %A" i (time.Value - newTime)
   time := newTime
   cloud { return System.DateTime.Now }
    |> cluster.CreateCloudTask ]

cluster.ShowCloudTaskInfo()