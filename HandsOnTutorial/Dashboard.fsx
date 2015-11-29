(*** hide ***)
//#load "ThespianCluster.fsx"
#load "AzureCluster.fsx"

#load @"..\packages\FSharp.Charting\FSharp.Charting.fsx"
#r "../packages/FSharp.Control.AsyncSeq/lib/net45/FSharp.Control.AsyncSeq.dll"
#load @"..\paket-files\fslaborg\FSharp.Charting\docs\content\EventEx-0.1.fsx"

open System
open MBrace.Core
open MBrace.Runtime
open FSharp.Charting

type ClusterStatusStream = IEvent<WorkerRef array>

let getLiveSeries handler createChart chartName (clusterStatusStream:ClusterStatusStream) =
    let series = clusterStatusStream |> Event.map handler
    createChart(series, chartName)

let getLiveSeriesPerWorker getField = getLiveSeries (fun workers -> workers |> Seq.map(fun worker -> worker.Id, getField worker))

let buildCombinedLiveChart getField createChart chartName =
    getLiveSeriesPerWorker getField createChart chartName >> Chart.WithTitle chartName

let getDefaultNullable nullable =
    nullable
    |> Option.ofNullable
    |> defaultArg <| 0.

let OpenDashboard (cluster:MBrace.Azure.AzureCluster) =
    let clusterStatusStream = Event.clock 500 |> Event.map(fun _ -> cluster.Workers)
    Chart.Rows [
        Chart.Columns [
            clusterStatusStream |> buildCombinedLiveChart (fun w -> w.ActiveWorkItems) (fun (a,b) -> LiveChart.Pie(a, Name = b)) "Active Work Items"
            clusterStatusStream |> buildCombinedLiveChart (fun w -> defaultArg (w.CpuUsage |> Option.ofNullable) 0.) (fun (a,b) -> LiveChart.Column(a, Name = b) |> Chart.WithYAxis(Max = 100., Min = 0.)) "CPU Usage" ]
        Chart.Columns [
            clusterStatusStream |> getLiveSeries (fun workers -> [ "All Workers", workers |> Seq.sumBy(fun w -> w.ActiveWorkItems) ]) (fun (a,b) -> LiveChart.Column(a, Name = b)) "Total Active Work Items"
            clusterStatusStream |> buildCombinedLiveChart (fun w -> getDefaultNullable w.NetworkUsageDown + getDefaultNullable w.NetworkUsageUp) (fun (a,b) -> LiveChart.Bar(a, Name = b) |> Chart.WithYAxis(Min = 0., Max = 5000., Title = "Kbps")) "Bandwith Utilization" ]
        ]


//let getLiveHistoricalSeries createChart getField seriesName (worker:WorkerRef) =
//    let series =
//        Event.clock 250
//        |> Event.map(fun _ ->
//            DateTime.UtcNow.ToShortTimeString(),
//            getField worker)
//        |> Event.scan(fun state ev -> (state @ [ ev ]) |> List.skip(state.Length - 60)) []
//    createChart(series, seriesName + worker.WorkerId.Id)
//
//let buildCombinedHistoricalLiveChart createChart getField chartName workers =
//    Chart.Combine (workers |> Array.map (getLiveHistoricalSeries createChart getField chartName))
//    |> Chart.WithTitle chartName
