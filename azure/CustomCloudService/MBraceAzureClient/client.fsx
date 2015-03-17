#load "../../../packages/MBrace.Azure.Client.0.6.4-alpha/bootstrap.fsx"

open MBrace
open MBrace.Azure
open MBrace.Azure.Client

let config = 
    { Configuration.Default with
        StorageConnectionString = "your connection string"
        ServiceBusConnectionString = "your connection string" }


let runtime = Runtime.GetHandle(config)
runtime.AttachClientLogger(new ConsoleLogger()) 


runtime.ShowWorkers()
runtime.ShowProcesses()

let helloJob = runtime.CreateProcess(cloud { return "Hello world"})
helloJob.AwaitResult()


let parallelJob =
    [1..10]
    |> List.map (fun i -> cloud { return i * i})
    |> Cloud.Parallel
    |> runtime.CreateProcess

parallelJob.ShowInfo()

parallelJob.AwaitResult()



let ps = 
    Cloud.ParallelEverywhere(cloud { return System.Environment.MachineName })
    |> runtime.CreateProcess

ps.AwaitResult()