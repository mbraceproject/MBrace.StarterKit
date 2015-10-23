(*** hide ***)
#load "ThespianCluster.fsx"
//#load "AzureCluster.fsx"

#r "../packages/FSharp.Data/lib/net40/FSharp.Data.dll"

#I "../packages/Google.DataTable.Net.Wrapper/lib"
#I "../packages/XPlot.GoogleCharts/lib/net45"
#I "../packages/XPlot.GoogleCharts.WPF/lib/net45"
#r "XPlot.GoogleCharts.dll"
#r "XPlot.GoogleCharts.WPF.dll"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit AzureCluster.fsx to enter your connection strings.

open System
open System.IO

open XPlot.GoogleCharts
open FSharp.Data

open MBrace.Core
open MBrace.Flow

// Initialize client object to an MBrace cluster
let cluster = Config.GetCluster() 

(**

# MBrace, CloudFlows and FSharp.Data – data analysis made easy

This sample has been taken from Isaac Abraham's [blog](https://cockneycoder.wordpress.com/2015/10/20/mbrace-cloudflows-and-fsharp-data-a-perfect-match/).

## Type Providers on MBrace

We’ll start by generating a schema for our data using FSharp.Data and its CSV Type Provider. 
Usually the type provider can infer all data types and columns but in this case the file does not include headers, so we’ll supply them ourselves. 
I’m also using a local version of the CSV file which contains a subset of the data (the live dataset even for a single month is > 10MB)

*)

type HousePrices = CsvProvider< @"../data/SampleHousePrices.csv", HasHeaders = true>

(**

In that single line, we now have a strongly-typed way to parse CSV data. 
Now, let’s move onto the MBrace side of things. 
I want to start with something simple – let’s get the average sale price of a property, by month, and chart it.

*)

let prices : (int * float) array =
    [ "http://publicdata.landregistry.gov.uk/market-trend-data/price-paid-data/a/pp-2015.csv" ]
    |> CloudFlow.OfHttpFileByLine                                                                    // Stream the HTTP file across the cluster
    |> CloudFlow.map (HousePrices.ParseRows >> Seq.head)                                             // Convert from raw text to our CSV Provided type
    |> CloudFlow.groupBy(fun row -> row.DateOfTransfer.Month)                                        // Group by month
    |> CloudFlow.map(fun (month, rows) -> month, rows |> Seq.averageBy (fun row -> float row.Price)) // Get the average price for each month
    |> CloudFlow.toArray
    |> cluster.Run

(**

A *CloudFlow* is an MBrace primitive which allows a distributed set of transformations to be chained together, 
just like you would with the Seq module in F# (or LINQ’s IEnumerable operators for the rest of the .NET world), 
except in MBrace, a CloudFlow pipeline is partitioned across the cluster, making full use of resources available in the cluster; 
only when the pipelines are completed in each partition are they aggregated together again.

Also notice that we’re using type providers *in tandem* with the distributed computation.
Once we call the ParseRows function, in the next call in the pipeline,
we’re working with a strongly-typed object model – so DateOfTransfer is a proper DateTime etc.
All dependent assemblies have automatically been shipped with MBrace;
it wasn’t explicitly designed to work with FSharp.Data – *it just works*.
So now that we have an array of int * float i.e. month * price, we can easily map it on a chart.

*)

prices
|> Seq.sortBy fst // sort by month
|> Seq.map(fun (month, price) -> sprintf "%s 2015" (System.DateTime(2015, month, 1).ToString("MMM")), price)
|> Chart.Line
|> Chart.WithOptions(Options(curveType = "function"))
|> Chart.Show

(**

Easy.

## Persisted Cloud Flows

Even better, MBrace supports something called Persisted Cloud Flows (known in the Spark world as RDDs). 
These are flows whose results are partitioned and cached across the cluster, ready to be re-used again and again. 
This is particularly useful if you have an intermediary result set that you wish to query multiple times. 
In our case, we might persist the first few lines of the computation (which involves downloading the data from source and parsing with the CSV Type Provider), 
ready to be used for any number of strongly-typed queries we might have: –

*)

// download data, convert to provided type and partition across nodes in-memory only
let persistedHousePrices =
    [ "http://publicdata.landregistry.gov.uk/market-trend-data/price-paid-data/a/pp-2015.csv" ]
    |> CloudFlow.OfHttpFileByLine
    |> CloudFlow.map (HousePrices.ParseRows >> Seq.head)
    |> CloudFlow.persist StorageLevel.Memory
    |> cluster.Run

// get average house price by month
let pricesByMonth =
    persistedHousePrices
    |> CloudFlow.groupBy(fun row -> row.DateOfTransfer.Month)
    |> CloudFlow.map(fun (month, rows) -> month, rows |> Seq.averageBy (fun row -> float row.Price))
    |> CloudFlow.toArray
    |> cluster.Run

// get property types in London
let londonProperties =
    persistedHousePrices
    |> CloudFlow.filter(fun row -> row.TownCity = "LONDON")
    |> CloudFlow.countBy(fun row -> row.PropertyType)
    |> CloudFlow.toArray
    |> cluster.Run

// 5 seconds - get % new builds by county
let newBuildsByCounty =
    persistedHousePrices
    |> CloudFlow.groupBy(fun row -> row.County)
    |> CloudFlow.map(fun (county, rows) ->
        let rows = rows |> Seq.toList
        let newBuilds = rows |> List.filter(fun r -> r.NewBuild = "Y") |> List.length
        let percentageNewBuilds = (100. / float rows.Length) * float newBuilds
        county, percentageNewBuilds)
    |> CloudFlow.toArray
    |> cluster.Run
    |> Array.sortByDescending snd

(**

etc, etc.

So notice that the first query takes 45 seconds to execute,
which involves downloading the data and parsing it via the CSV type provider. 
Once we’ve done that, we persist it across the cluster in memory – 
then we can re-use that persisted flow in all subsequent queries, each of which just takes a few seconds to run.

*)