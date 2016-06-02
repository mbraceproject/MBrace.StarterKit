#r "../../packages/Suave/lib/net40/Suave.dll"
#r "../../packages/MBrace.Core/lib/net45/MBrace.Core.dll"
#r "Microsoft.WindowsAzure.ServiceRuntime.dll"

#nowarn "443"

open System
open System.IO
open MBrace.Core
open Suave
open Suave.Types
open Suave.Http
open Suave.Http.Applicatives
open Suave.Http.Successful
open Suave.Web
open Microsoft.WindowsAzure.ServiceRuntime

let suaveServerInCloud endPointName mkApp = 
  cloud { 
    do! Cloud.Logf "starting web server job..." 

    // Get the endpoint we have to fulfill
    let httpEP = RoleEnvironment.CurrentRoleInstance.InstanceEndpoints.[endPointName].IPEndpoint

    do! Cloud.Logf "got endpoint %A..." httpEP

    // Configure Suave logging to do a local capture
    let log = ResizeArray<Logging.LogLine>()
    let logger =
        { new Suave.Logging.Logger with
            member x.Log level fLine = log.Add(fLine()) }
    let logLines() = 
        [ for ll in log -> 
            let err = match ll.``exception`` with | None -> "" | Some e -> e.ToString()
            err + " " + ll.message + " " + ll.level.ToString() ]

    // Cancel the server when the cloud process gets cancelled
    let! r = Cloud.CancellationToken
    let cts = r.LocalToken 

    // Configure Suave
    let binding = HttpBinding.mk Protocol.HTTP httpEP.Address (uint16 httpEP.Port)
    let config = { defaultConfig  with 
                     bindings = [ binding ]
                     logger = logger
                     cancellationToken = cts }

    //let! ctx = Cloud.GetExecutionContext()

    do! Cloud.Logf "starting..." 

    // Start the Suave server, binding to the endpoint
    do Async.Start (async { startWebServer config (mkApp()) })

    // Wait for cancellation
    do! Cloud.Logf "waiting for cancellation..." 
    while true do 
       do! Cloud.Sleep 1000
       for ll in logLines() do 
          do! Cloud.Log ll
       log.Clear()
    }

module Angular = 

    let header = """<head>
    <link rel="stylesheet" href="http://maxcdn.bootstrapcdn.com/bootstrap/3.2.0/css/bootstrap.min.css">
    <script src="http://ajax.googleapis.com/ajax/libs/angularjs/1.2.26/angular.min.js"></script>
    </head>"""

    let table hs (f: 'T -> #seq<string>) (xs: seq<'T>) = 
       [  yield """  <table class="table table-striped">"""
          yield """   <thead><tr>"""
          for h in hs do 
              yield (sprintf "<th>%s</th>" h)
          yield """</tr></thead>"""
          yield """   <tbody>"""
          for x in xs do
             yield """<tr>"""
             for e in f x do 
                yield """<td>"""
                yield e 
                yield """</td>"""
             yield """</tr>"""
          yield """   </tbody>"""
          yield """  </table>""" ]


