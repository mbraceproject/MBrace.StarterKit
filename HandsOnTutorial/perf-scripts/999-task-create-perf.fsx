(*** hide ***)
#load "../ThespianCluster.fsx"
//#load "../AzureCluster.fsx"
//#load "../AwsCluster.fsx"

#time "on"

open System
open System.IO
open MBrace.Core
open MBrace.Flow

(** 
# Helpers for measuring performance characteristics of your cluster 

First initialize client object to an MBrace cluster:
*)

let cluster = Config.GetCluster() 


(** 
To measure task creation perf:
*)
let ps = 
 [let time = ref System.DateTime.Now.TimeOfDay
  for i in 0 .. 10000 ->
   let newTime = System.DateTime.Now.TimeOfDay
   printfn "starting %d, diff = %A" i (time.Value - newTime)
   time := newTime
   cloud { return System.DateTime.Now }
    |> cluster.CreateProcess ]

cluster.ShowProcesses()

(** 
To get the version of MBrace.Core running on the cluster:
*)
cloud { return typeof<Cloud<int>>.Assembly.GetName().Version.ToString() } |> cluster.Run


(** 
To get the version of MBrace.Flow running on the cluster
*)
cloud { return typeof<CloudFlow<int>>.Assembly.GetName().Version.ToString() } |> cluster.Run

(** 
To compare the CPU performance of a single core on your scripting client v. cluster:
*)

module CompareOneCoreCpuPerf = 
    // Helper to run hot for about 10 seconds
    let hot() = 
        let x = ref 0
        for i in 1 .. 100000 do for j in 1 .. 10000 do x := x.Value + 1
        x.Value 


    hot() 

    let hotTask = cloud { return hot() } |> cluster.CreateProcess

    hotTask.ShowInfo()
    hotTask.ExecutionTime 

