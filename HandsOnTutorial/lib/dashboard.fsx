#r "PresentationCore.dll"
#r "PresentationFramework.dll"
#r "WindowsBase.dll"
#I "../../packages/"
#r "MBrace.Core/lib/net45/MBrace.Core.dll"
#r "MBrace.Runtime/lib/net45/MBrace.Runtime.dll"
#r "Vagabond/lib/net45/Vagabond.dll"
#r "MBrace.Azure/lib/net45/MBrace.Azure.dll"
#r "MBrace.Azure.Management/lib/net45/MBrace.Azure.Management.dll"
#load "FSharp.Charting/FSharp.Charting.fsx"
#load "../paket-files/fslaborg/FSharp.Charting/docs/content/EventEx-0.1.fsx"

open FSharp.Charting
open System
open System.Threading
open System.Windows
open System.Windows.Controls
open System.Windows.Media.Imaging
open System.Windows.Forms.DataVisualization.Charting
open System.Drawing
open MBrace.Core
open MBrace.Runtime
open MBrace.Azure.Management

module private Event =

    let mkPoller interval getter =
        let cts = new CancellationTokenSource()
        let event = new Event<_>()
        let rec poller () = async {
            let! result = getter () |> Async.Catch
            match result with
            | Choice1Of2 info -> try event.Trigger info with _ -> ()
            | Choice2Of2 _ -> ()
            do! Async.Sleep interval
            return! poller()
        }

        Async.Start(poller(), cts.Token)
        cts, event.Publish
        

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

    let showWithCancellation (cts : CancellationTokenSource) (chart : ChartTypes.GenericChart) =
        let form = chart.ShowChart()
        let _ = form.Closing.Subscribe(fun _ -> cts.Cancel())
        { new IDisposable with member __.Dispose() = cts.Cancel() ; form.Close() }

[<AutoOpen>]
module Dashboard =

    type private WorkerInfo =
        {
            Id : string
            MemoryUsage : float
            TotalMemory : float
            CpuUsage    : float
            ActiveWorkItems : int
            NetworkUsageUp : float
            NetworkUsageDown : float
        }

    type MBraceClient with
        member cluster.OpenDashboard() =
            let getWorkerInfo (w : WorkerRef) =
                let (!) (n : Nullable<float>) = n.GetValueOrDefault 0.
                {
                    Id = w.Id
                    MemoryUsage = ! w.MemoryUsage
                    TotalMemory = ! w.TotalMemory
                    CpuUsage    = ! w.CpuUsage
                    ActiveWorkItems = w.ActiveWorkItems
                    NetworkUsageUp = ! w.NetworkUsageDown
                    NetworkUsageDown = ! w.NetworkUsageDown
                }

            let cts, stream = Event.mkPoller 500 (fun () -> async { return cluster.Workers |> Array.map getWorkerInfo })

            Chart.Rows [
                Chart.Columns [
                    stream |> Event.map (fun workers ->
                        let inUse = workers |> Seq.sumBy(fun w -> w.MemoryUsage)
                        let total = workers |> Seq.sumBy(fun w -> w.TotalMemory)
                        [ ("In Use", inUse);
                            ("Available", total - inUse) ]) |> ChartBuilders.asLiveChart (fun (a,b) -> LiveChart.Pie(a, Name = b) |> Chart.With3D()) "Cluster Memory"
                    stream |> Event.map (fun ws -> ws |> Seq.map (fun w -> w.Id, w.CpuUsage)) |> ChartBuilders.asLiveChart (fun (a,b) -> LiveChart.Column(a, Name = b) |> Chart.WithYAxis(Min = 0., Max = 100.)) "CPU Usage" ]
                Chart.Columns [
                    stream |> Event.map (fun workers -> DateTime.Now.ToShortTimeString(), workers |> Seq.sumBy(fun w -> w.ActiveWorkItems)) |> Event.windowAtMost 60 |> ChartBuilders.asLiveChart (fun (a, b) -> LiveChart.Line(a, Name = b) |> Chart.WithYAxis(Min = 0., Max = 100.)) "Total Active Work Items"
                    stream |> Event.map (fun ws -> ws |> Seq.map (fun w -> w.Id, w.NetworkUsageDown + w.NetworkUsageUp)) |> ChartBuilders.asLiveChart (fun (a,b) -> LiveChart.Bar(a, Name = b) |> Chart.WithYAxis(Min = 0., Max = 5000., Title = "Kbps")) "Bandwith Utilization" ]
                ]

            |> ChartBuilders.showWithCancellation cts

    type Deployment with
        member deployment.OpenDashboard() =
            let cts, stream = Event.mkPoller 5000 (fun () -> deployment.GetInfoAsync())
            let progressPie () = 
                stream
                |> Event.map (fun info -> 
                        match info.DeploymentState with 
                        | Provisioning pct -> [("", 1. - pct); (sprintf "%2.2f%% Complete" (100. * pct), pct)]  
                        | s -> [(string s, 100.)])

                |> ChartBuilders.asLiveChart (fun (a,b) -> LiveChart.Pie(a, Name = b) |> Chart.With3D()) "Provision Progress (Cluster)"

            let nodeChart () =
                stream
                |> Event.map (fun info ->
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

                    info.VMInstances |> Seq.map (fun n -> sprintf "%s\n(%s)" n.Id n.Status, getNodePct n) |> Seq.sortByDescending fst)

                |> ChartBuilders.asLiveChart(fun (a,b) -> 
                        LiveChart.Bar(a, Name = b, YTitle = "%Complete")
                        |> Chart.WithXAxis(MajorGrid = ChartBuilders.dashGrid)
                        |> Chart.WithYAxis(MajorGrid = ChartBuilders.dashGrid, Max = 100., Min = 0.) 
                        |> Chart.WithStyling(Color = Color.BurlyWood)) "Provision Progress (VMs)"

            Chart.Columns [nodeChart () ; progressPie ()]
            |> Chart.WithTitle (sprintf "Cluster %A" deployment.ServiceName)
            |> ChartBuilders.showWithCancellation cts