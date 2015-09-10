(*** hide ***)
#load "ThespianCluster.fsx"
#load "AzureCluster.fsx"

open System
open System.IO
open MBrace.Core
open MBrace.Azure
open MBrace.Flow

// Initialize client object to an MBrace cluster:
let cluster = 
//    getAzureClient() // comment out to use an MBrace.Azure cluster; don't forget to set the proper connection strings in Azure.fsx
    initThespianCluster(4) // use a local cluster based on MBrace.Thespian; configuration can be adjusted using Thespian.fsx

let ps = 
 [let time = ref System.DateTime.Now.TimeOfDay
  for i in 0 .. 10000 ->
   let newTime = System.DateTime.Now.TimeOfDay
   printfn "starting %d, diff = %A" i (time.Value - newTime)
   time := newTime
   cloud { return System.DateTime.Now }
    |> cluster.CreateCloudTask ]

cluster.ShowCloudTaskInfo()