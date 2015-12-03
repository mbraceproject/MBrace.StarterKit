#I @"..\..\packages"
#r @"MBrace.Core\lib\net45\MBrace.Core.dll"
#r @"MBrace.Runtime\lib\net45\MBrace.Runtime.dll"
#r "FSharp.Control.AsyncSeq/lib/net45/FSharp.Control.AsyncSeq.dll"
#r "Vagabond/lib/net45/Vagabond.dll"
#load @"FSharp.Charting\FSharp.Charting.fsx"
#load @"..\paket-files\fslaborg\FSharp.Charting\docs\content\EventEx-0.1.fsx"
#r "PresentationCore"
#r "PresentationFramework"
#r "WindowsBase"
#r "System.Xaml"
#r @"MBrace.Azure\lib\net45\MBrace.Azure.dll"
#r @"MBrace.Azure.Management\lib\net45\MBrace.Azure.Management.dll"

open FSharp.Charting
open System
open System.Windows
open System.Windows.Controls
open System.Windows.Media.Imaging
open System.Windows.Forms.DataVisualization.Charting
open System.Drawing
open MBrace.Core
open MBrace.Runtime
open MBrace.Azure.Management


Window().Close()

type private ClusterStatusStream = IEvent<WorkerRef array>

module private Event =
    let mapWorker getField =
        Event.map(snd >> Seq.map(fun (worker:WorkerRef) -> worker.Id, getField worker))

module private ChartBuilders =
    let stylize chartName = Chart.WithTitle(Text = chartName, InsideArea = false)
    let constructHistoricalSeries mapper =
        Event.map mapper
        >> Event.scan(fun state ev ->
            (state @ [ ev ])
            |> List.skip(state.Length - 60)) []
    let asLiveChart createChart chartName stream =
        createChart(stream, chartName) |> stylize chartName

    let dashGrid = ChartTypes.Grid(LineColor = Color.Gainsboro, LineDashStyle = ChartDashStyle.Dash)

let OpenDashboard (cluster:MBraceClient) =
    let getNullable nullable =
        nullable
        |> Option.ofNullable
        |> defaultArg <| 0.

    let stream = Event.clock 500 |> Event.map(fun time -> time, cluster.Workers)

    Chart.Rows [
        Chart.Columns [
            stream |> Event.map (fun (_, workers) ->
                let inUse = workers |> Seq.sumBy(fun w -> getNullable w.MemoryUsage)
                let total = workers |> Seq.sumBy(fun w -> getNullable w.TotalMemory)
                [ "In Use", inUse
                  "Available", total - inUse ]) |> ChartBuilders.asLiveChart (fun (a,b) -> LiveChart.Pie(a, Name = b) |> Chart.With3D()) "Cluster Memory"
            stream |> Event.mapWorker (fun worker -> getNullable worker.CpuUsage) |> ChartBuilders.asLiveChart (fun (a,b) -> LiveChart.Column(a, Name = b) |> Chart.WithYAxis(Min = 0., Max = 100.)) "CPU Usage" ]
        Chart.Columns [
            stream |> Event.map (fun (time, workers) -> time.ToShortTimeString(), workers |> Seq.sumBy(fun w -> w.ActiveWorkItems)) |> Event.windowAtMost 60 |> ChartBuilders.asLiveChart (fun (a, b) -> LiveChart.Line(a, Name = b) |> Chart.WithYAxis(Min = 0., Max = 100.)) "Total Active Work Items"
            stream |> Event.mapWorker (fun w -> getNullable w.NetworkUsageDown + getNullable w.NetworkUsageUp) |> ChartBuilders.asLiveChart (fun (a,b) -> LiveChart.Bar(a, Name = b) |> Chart.WithYAxis(Min = 0., Max = 5000., Title = "Kbps")) "Bandwith Utilization" ]
        ]

let OpenDeploymentDashboard (deployment : Deployment) =
    let stream = Event.clock 5000 |> Event.choose(fun _ -> try Some(deployment.Nodes, deployment.DeploymentState) with _ -> None)

    let progressPie () = 
        stream 
        |> Event.map (fun (_,state) -> 
                match state with 
                | Provisioning pct -> [("", 1. - pct); ("%Complete", pct)]  
                | _ -> [(string state, 100.)])

        |> ChartBuilders.asLiveChart (fun (a,b) -> LiveChart.Pie(a, Name = b) |> Chart.With3D()) "Provision Progress (Cluster)"

    let nodeChart () =
        stream
        |> Event.map (fun (nodes,_) ->
            let getNodePct (node : VMInstance) =
                let score =
                    match node.Status with
                    | "StoppedVM"           -> 1
                    | "CreatingVM"          -> 2
                    | "StartingVM"          -> 3
                    | "RoleStateUnknown"    -> 4
                    | "BusyRole"            -> 5
                    | "ReadyRole"           -> 6
                    | _                     -> 0

                float score * 100. / 6.

            nodes |> Seq.map (fun n -> sprintf "%s\n(%s)" n.Id n.Status, getNodePct n) |> Seq.sortByDescending fst)

        |> ChartBuilders.asLiveChart(fun (a,b) -> 
                LiveChart.Bar(a, Name = b, YTitle = "%Complete")
                |> Chart.WithXAxis(MajorGrid = ChartBuilders.dashGrid)
                |> Chart.WithYAxis(MajorGrid = ChartBuilders.dashGrid, Max = 100., Min = 0.) 
                |> Chart.WithStyling(Color = Color.BurlyWood)) "Provision Progress (VMs)"

    Chart.Columns [nodeChart () ; progressPie ()]
    |> Chart.WithTitle (sprintf "Cluster %A" deployment.ServiceName)