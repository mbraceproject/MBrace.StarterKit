
#load "../../../FSharpDemoScripts/extlib/EventEx-0.1.fsx"
#load "../../../FSharpDemoScripts/packages/FSharp.Charting/FSharp.Charting.fsx"
#load "../../../FSharpDemoScripts/vizlib/load-wpf.fsx"
#load "../../../FSharpDemoScripts/vizlib/show.fsx"

open System
open System.IO
open MBrace.Core
open MBrace.Azure
open MBrace.Azure.Client
open MBrace.Flow

//-----------------------------------------------
// First some data scripting



let tableOfSquares = 
    [ for i in 0 .. 99 -> (i, i*i)  ] 


tableOfSquares |> showGrid

//-----------------------------------------------
// Now some charting

open FSharp.Charting


Chart.Line [ for i in 0 .. 99 -> (i, i*i) ]
Chart.Pie [ for i in 0 .. 99 -> (i, i*i) ]
Chart.ErrorBar [ for i in 0.0 .. 3.1 .. 100.0 -> (i, i*i, i*i*0.90, i*i*1.10) ]


let rnd = System.Random()
let rand() = rnd.NextDouble()

Chart.Point [ for i in 0 .. 10000 -> (rand(),rand()*rand()) ]
    

//------------------------------------------------------------------------------------

#load "credentials.fsx"
#load "lib/collections.fsx"
#load "lib/sieve.fsx"

(** First you connect to the cluster: *) using a configuration to bind to your storage and service bus on Azure.

let cluster = Runtime.GetHandle(config)

// Optionally, attach console logger to client object
//cluster.AttachClientLogger(new ConsoleLogger())

// We can connect to the cluster and get details of the workers in the pool etc.
cluster.ShowWorkers()

// We can view the history of processes
cluster.ShowProcesses()

// Execute a cloud workflow and get a handle to the running job
let job = 
    cloud { return "Hello world!" } 
    |> cluster.CreateProcess

// You can evaluate helloWorldProcess to get details on it
let isJobComplete = job.Completed

// Block until the result is computed by the cluster
let text = job.AwaitResult()

// Alternatively we can do this all in one line
let quickText = 
    cloud { return "Hello world!" } 
    |> cluster.Run

//---------------------------------
// CPU intensive job

let clusterPrimesJob =
    [ for i in 1 .. 100 -> 
         cloud { 
            let primes = Sieve.getPrimes 10000000
            return sprintf "calculated %d primes %A on machine '%s'" primes.Length primes Environment.MachineName 
         } 
    ]
    |> Cloud.Parallel
    |> cluster.CreateProcess



//---------------------------------

// We can test a workflow by running locally using async semantics
let localResult =
    cloud { printfn "hello, world" ; return Environment.MachineName }
    |> cluster.RunLocally

// Compare behaviour against remote execution
let remoteResult =
    cloud { printfn "hello, world" ; return Environment.MachineName }
    |> cluster.Run


let form = new System.Windows.Forms.Form(Visible=true,TopMost=true)

form.MouseMove
   |> Event.map (fun e -> e.X, 500-e.Y) 
   |> LiveChart.LineIncremental

form.MouseMove
   |> Event.map (fun e -> System.DateTime.Now, 500-e.Y) 
   |> LiveChart.LineIncremental

form.MouseMove 
    |> Event.map (fun e -> e.Y) 
    |> Event.sampled 30 
    |> Event.windowTimeInterval 3000
    |> LiveChart.Line
