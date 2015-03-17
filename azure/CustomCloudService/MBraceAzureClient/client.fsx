#load "../../../packages/MBrace.Azure.Client/bootstrap.fsx"

open MBrace
open MBrace.Azure
open MBrace.Azure.Client


// configuration object used for connecting with your Azure cluster
let config = 
    { Configuration.Default with
        StorageConnectionString = "your connection string"
        ServiceBusConnectionString = "your connection string" }


// gets a runtime handle to runtime of provided configuration
let runtime = Runtime.GetHandle(config)
runtime.AttachClientLogger(new ConsoleLogger()) 


runtime.ShowWorkers() // prints all workers in cluster
runtime.ShowProcesses() // prints all processes running or completed in cluster

// create a trivial process that returns a string
let helloJob = runtime.CreateProcess(cloud { return "Hello world"})
helloJob.AwaitResult()


// create a process that executes 10 workflows in parallel in the cluster
let parallelJob =
    [ for i in 1 .. 10 -> cloud { return i * i } ]
    |> Cloud.Parallel
    |> runtime.CreateProcess

parallelJob.ShowInfo()

parallelJob.AwaitResult()


// creates a process that executes a computation on every worker on the cluster in parallel
let ps = 
    Cloud.ParallelEverywhere(cloud { return System.Environment.MachineName })
    |> runtime.CreateProcess

ps.AwaitResult()