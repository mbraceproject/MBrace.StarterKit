#load "credentials.fsx"
#load "lib/sieve.fsx"

open System
open System.IO
open MBrace.Core
open MBrace.Azure
open MBrace.Azure.Client
open MBrace.Azure.Runtime
open MBrace.Workflows


(**
 This tutorial illustrates creating a process which 
 acts as a web server to monitor the cluster.
**)

System.IO.Directory.CreateDirectory( __SOURCE_DIRECTORY__ + "/999-web-server")
System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__ + "/999-web-server"

if not (File.Exists "paket.exe") then
    let url = "https://github.com/fsprojects/Paket/releases/download/0.27.2/paket.exe" in use wc = new System.Net.WebClient() in let tmp = Path.GetTempFileName() in wc.DownloadFile(url, tmp); File.Move(tmp,"paket.exe");;

#r "999-web-server/paket.exe"

Paket.Dependencies.Install """
    source https://nuget.org/api/v2
    nuget Suave
""";;

#load "webserver.fsx"

open Suave
open Suave.Types
open Suave.Http
open Suave.Http.Applicatives
open Suave.Http.Successful
open Suave.Web

open Webserver

// This is the Suave specification for the web server we will run in the cluster
let webServerSpec () =

  let hello ctxt = 
     async { return! OK "hello" ctxt }

  let getCluster() = 
     Runtime.GetHandle(config) 

  // This Suave request is executed in response to a GET on 
  //   http://site/cluster/workers
  //
  // It uses cluster.GetWrokers to access information from the cluster
  let getWorkersRequest ctxt = 
     async { let cluster = getCluster()
             let workers = cluster.GetWorkers() |> Seq.toArray
             let msg = 
                 [ yield "<html>"
                   yield Angular.header
                   yield "<body>"
                   yield! workers |> Angular.table ["ID";"Heartbeat"] (fun w -> 
                       [ w.Id; sprintf "%A" w.HeartbeatTime ])
                   yield "</body>"
                   yield "</html>"
                 ]
                 |> String.concat "\n"
             let! rsp = OK msg ctxt 
             return rsp}

  // This Suave request is executed in response to a GET on 
  //   http://site/cluster/logs
  //
  // It uses cluster.GetLogs to access information from the cluster
  let getLogsRequest ctxt = 
     async { let cluster = getCluster()
             let logs = cluster.GetLogs()
             let msg = 
                 [ yield "<html>"
                   yield Angular.header
                   yield "<body>"
                   yield! logs |> Angular.table ["Time";"Message"] (fun w -> 
                       [ sprintf "%A" w.Time; w.Message ])
                   yield "</body>"
                   yield "</html>"
                 ]
                 |> String.concat "\n"
             return! OK msg ctxt  }

  // This Suave request is executed in response to a GET on 
  //   http://site/cluster/submit/primes/%d
  //
  // It uses cluster.CreateProcess to create a new job in the cluster.
  let computePrimesRequest n ctxt = 
     async { let cluster = getCluster()
             let logs = cluster.GetLogs()
             let job = 
               cluster.CreateProcess 
                (cloud { let primes = Sieve.getPrimes n
                         return sprintf "calculated %d primes: %A" primes.Length primes })
             let msg = 
                 [ yield "<html>"
                   yield Angular.header
                   yield "<body>"
                   yield (sprintf "<p>Created job %s</p>" job.Id)
                   yield "</body>"
                   yield "</html>"
                 ]
                 |> String.concat "\n"
             return! OK msg ctxt  }

  // This Suave request is executed in response to a GET on 
  //   http://site/cluster/job/%d
  //
  // It uses cluster.GetProcess to create a new job in the cluster.
  let getJobRequest v ctxt = 
     async { let cluster = getCluster()
             let logs = cluster.GetLogs()
             let job = cluster.GetProcess(v)
             let msg = 
                 [ yield "<html>"
                   yield Angular.header
                   yield "<body>"
                   yield (sprintf "<p>Job %s, Completed: %A, Result: %s</p>" job.Id job.Completed (try if job.Completed then sprintf "%A" (job.AwaitResultBoxed()) else "" with _ -> "<err>") )
                   yield "</body>"
                   yield "</html>"
                 ]
                 |> String.concat "\n"
             return! OK msg ctxt  }

  choose
    [ GET >>= choose
                [ path "/" >>= OK "Welcome to the cluster" 
                  path "/cluster/workers" >>= getWorkersRequest
                  path "/cluster/logs" >>= getLogsRequest
                  pathScan "/cluster/job/%s" getJobRequest
                  pathScan "/cluster/submit/primes/%d" computePrimesRequest ]
      POST >>= choose
                [ path "/hello" >>= OK "Hello POST"
                  path "/goodbye" >>= OK "Good bye POST" ] ]

let cluster = Runtime.GetHandle(config)

cluster.ShowProcesses()

// Use this to inspect the endpoints we can bind to in the cluster
let endPointNames = 
    cloud { return Microsoft.WindowsAzure.ServiceRuntime.RoleEnvironment.CurrentRoleInstance.InstanceEndpoints.Keys |> Seq.toArray }
    |> cluster.Run

// By default, MBrace clusters created using Brisk engine on Azure allow us to bind to 
//    - HTTP endpoint 'DefaultHttpEndpoint'
//    - HTTP endpoint 'MBraceStats'
//    - TCP endpoint 'DefaultTcpEndpoint'
//
// Here we bind to DefaultHttpEndpoint.  
let serverJob = suaveServerInCloud "DefaultHttpEndpoint" webServerSpec |> cluster.CreateProcess

// After you start the webserver (by binding to this internal endpoint), the website will 
// be published to the corresponding public endpoint. You can find the IP
// address or URL for the public endpoint by looking for 'Input Endpoints'
// in the 'Configuration' section of the Azure management console page for the 
// Cloud Service for the MBrace cluster.
//
// For example, the public URL for the MBraceWorkerRole may be http://191.233.103.54:80



// Use this to inspect the status of the web server job:
serverJob.ShowInfo()

// If the webserver doesn't start, then use this to see the logging output
// from webserver.fsx

serverJob.ShowLogs()

// Use this to cancel the web server (via the distributed cancellationToken being cancelled)
// Cancel the web server before trying to start another one.
// serverJob.Kill()

// Use this to wait for the web server to exit after cancellation
// serverJob.AwaitResult()




