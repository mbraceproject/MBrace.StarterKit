(*** hide ***)
#load "ThespianCluster.fsx"
//#load "AzureCluster.fsx"

#load @"..\packages\FSharp.Charting\FSharp.Charting.fsx"
#r "../packages/FSharp.Control.AsyncSeq/lib/net45/FSharp.Control.AsyncSeq.dll"
#load @"..\paket-files\fslaborg\FSharp.Charting\docs\content\EventEx-0.1.fsx"
#r "PresentationCore"
#r "PresentationFramework"
#r "WindowsBase"
#r "System.Xaml"

open System
open System.Windows
open System.Windows.Controls
open System.Windows.Media.Imaging

open System
open FSharp.Charting

open MBrace.Core
open MBrace.Runtime



let stylize chartName = Chart.WithTitle(Text = chartName, InsideArea = false)
Window().Close()

type ClusterStatusStream = IEvent<WorkerRef array>

module Event =
    let mapWorker getField =
        Event.map(snd >> Seq.map(fun (worker:WorkerRef) -> worker.Id, getField worker))
    let asTimeSeries n =
        Event.scan(fun state ev ->
            (state @ [ ev ])
            |> List.skip(state.Length - n)) []

module ChartBuilders =
    let constructHistoricalSeries mapper =
        Event.map mapper
        >> Event.scan(fun state ev ->
            (state @ [ ev ])
            |> List.skip(state.Length - 60)) []
    let asLiveChart createChart chartName stream =
        createChart(stream, chartName) |> stylize chartName
let private getNullable nullable =
    nullable
    |> Option.ofNullable
    |> defaultArg <| 0.

let OpenDashboard (cluster:MBraceClient) =
    let stream = Event.clock 500 |> Event.map(fun time -> time, cluster.Workers)

    Chart.Rows [
        Chart.Columns [
            stream |> Event.mapWorker (fun worker -> worker.ActiveWorkItems) |> ChartBuilders.asLiveChart (fun (a,b) -> LiveChart.Pie(a, Name = b) |> Chart.With3D()) "Active Work Items"
            stream |> Event.mapWorker (fun worker -> defaultArg (worker.CpuUsage |> Option.ofNullable) 0.) |> ChartBuilders.asLiveChart (fun (a,b) -> LiveChart.Column(a, Name = b) |> Chart.WithYAxis(Min = 0., Max = 100.)) "CPU Usage" ]
        Chart.Columns [
            stream |> Event.map (fun (time, workers) -> time.ToShortTimeString(), workers |> Seq.sumBy(fun w -> w.ActiveWorkItems)) |> Event.asTimeSeries 60 |> ChartBuilders.asLiveChart (fun (a, b) -> LiveChart.Line(a, Name = b)) "Total Active Work Items"
            stream |> Event.mapWorker (fun w -> getNullable w.NetworkUsageDown + getNullable w.NetworkUsageUp) |> ChartBuilders.asLiveChart (fun (a,b) -> LiveChart.Bar(a, Name = b) |> Chart.WithYAxis(Min = 0., Max = 5000., Title = "Kbps")) "Bandwith Utilization" ]
        ]