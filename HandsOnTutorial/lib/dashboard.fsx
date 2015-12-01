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

open FSharp.Charting
open System
open System.Windows
open System.Windows.Controls
open System.Windows.Media.Imaging
open MBrace.Core
open MBrace.Runtime

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
    let private stylize chartName = Chart.WithTitle(Text = chartName, InsideArea = false)
    let constructHistoricalSeries mapper =
        Event.map mapper
        >> Event.scan(fun state ev ->
            (state @ [ ev ])
            |> List.skip(state.Length - 60)) []
    let asLiveChart createChart chartName stream =
        createChart(stream, chartName) |> stylize chartName

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
            stream |> Event.map (fun (time, workers) -> time.ToShortTimeString(), workers |> Seq.sumBy(fun w -> w.ActiveWorkItems)) |> Event.asTimeSeries 60 |> ChartBuilders.asLiveChart (fun (a, b) -> LiveChart.Line(a, Name = b) |> Chart.WithYAxis(Min = 0., Max = 100.)) "Total Active Work Items"
            stream |> Event.mapWorker (fun w -> getNullable w.NetworkUsageDown + getNullable w.NetworkUsageUp) |> ChartBuilders.asLiveChart (fun (a,b) -> LiveChart.Bar(a, Name = b) |> Chart.WithYAxis(Min = 0., Max = 5000., Title = "Kbps")) "Bandwith Utilization" ]
        ]
