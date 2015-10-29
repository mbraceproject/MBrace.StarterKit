(*** hide ***)
#load "../ThespianCluster.fsx"
//#load "../AzureCluster.fsx"


#r "../../packages/FSharp.Data/lib/net40/FSharp.Data.dll"

#I "../../packages/Google.DataTable.Net.Wrapper/lib"
#I "../../packages/XPlot.GoogleCharts/lib/net45"
#I "../../packages/XPlot.GoogleCharts.WPF/lib/net45"
#r "XPlot.GoogleCharts.dll"
#r "XPlot.GoogleCharts.WPF.dll"

#load "../lib/utils.fsx"

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

# Combining CloudFlows with FSharp.Data to Analyze Historical UK House Price Data 

This sample has been taken from Isaac Abraham's [blog](https://cockneycoder.wordpress.com/2015/10/20/mbrace-cloudflows-and-fsharp-data-a-perfect-match/).

## Type Providers on MBrace

We’ll start by generating a schema for our data using FSharp.Data and its CSV Type Provider. 
Usually the type provider can infer all data types and columns but in this case the file does not include headers, so we’ll supply them ourselves. 
I’m also using a local version of the CSV file which contains a subset of the data (the live dataset even for a single month is > 10MB)

*)

type HousePrices = CsvProvider< @"../../data/SampleHousePrices.csv", HasHeaders = true>

(**

In that single line, we now have a strongly-typed way to parse CSV data. 
Now, let’s move onto the MBrace side of things. 
You start start with something simple – let’s get the average sale price of a property, by month, and chart it.

First, the input data.  Each of these files is ~70MB
*)
let small = false

let sources = 
  if small then 
    [ "https://raw.githubusercontent.com/mbraceproject/MBrace.StarterKit/master/data/SampleHousePriceFile.csv" ]
  else
    [// "http://publicdata.landregistry.gov.uk/market-trend-data/price-paid-data/b/pp-2012-part1.csv"
      "http://publicdata.landregistry.gov.uk/market-trend-data/price-paid-data/a/pp-2012.csv"
      "http://publicdata.landregistry.gov.uk/market-trend-data/price-paid-data/a/pp-2013.csv"
      "http://publicdata.landregistry.gov.uk/market-trend-data/price-paid-data/a/pp-2014.csv"
      "http://publicdata.landregistry.gov.uk/market-trend-data/price-paid-data/a/pp-2015.csv" 
    ]


(**
Next you stream the data source from the original web location and grab the first 10 lines:
*)


let sampleData =
    sources
    |> CloudFlow.OfHttpFileByLine
    |> CloudFlow.take 10
    |> CloudFlow.toArray
    |> cluster.Run

(**
Next, again stream the data source from the original web location and 
across the cluster, then convert the raw text to our CSV provided type.
Entries are grouped by month and the average price for each month is computed.

A *CloudFlow* is an MBrace primitive which allows a distributed set of transformations to be chained together, 
just like you would with the Seq module in F# (or LINQ’s IEnumerable operators for the rest of the .NET world), 
except in MBrace, a CloudFlow pipeline is partitioned across the cluster, making full use of resources available in the cluster; 
only when the pipelines are completed in each partition are they aggregated together again.

*)

let pricesTask =
    sources
    |> CloudFlow.OfHttpFileByLine
    |> CloudFlow.collect HousePrices.ParseRows
    |> CloudFlow.averageByKey 
            (fun row -> row.DateOfTransfer.Year, row.DateOfTransfer.Month) // the key of the groups
            (fun row -> float row.Price) // the statistic to average
    |> CloudFlow.toArray
    |> cluster.CreateProcess


(** 

Now observe the progress. Time will depend on download speeds to your data center or location.  
For the large data sets above you can expect approximately 2 minutes.

While you're waiting, notice that you're using type providers *in tandem* with cloud computations.
Once we call the ParseRows function, in the next call in the pipeline,
we’re working with a strongly-typed object model – so DateOfTransfer is a proper DateTime etc.

For example, if you hit "." after "row" you will see the available information 
includes ``Locality``, ``Price``, ``Street``, ``Postcode`` and so on.

In addition, all dependent assemblies have automatically been shipped with MBrace.

MBrace wasn’t explicitly designed to work with FSharp.Data and F# type providers – *it just works*.

*)

pricesTask.ShowInfo()
cluster.ShowWorkers()

(** 
Now wait for the results. 
*)

let prices = pricesTask.Result

(**

Now that you have an array of year, month and price data, we can easily map it on a chart.

*)

let formatYearMonth (year,month) = sprintf "%s" (DateTime(year, month, 1).ToString("yyyy-MMM"))

let chartPrices prices = 
    prices
    |> Seq.sortBy fst // sort by year, month
    |> Seq.map(fun (ym, price) -> formatYearMonth ym, price)
    |> Chart.Line
    |> Chart.WithOptions(Options(curveType = "function"))
    |> Chart.Show

chartPrices prices
(**

Easy.

## Persisted Cloud Flows

To prevent repeated work, MBrace supports something called Persisted Cloud Flows (known in the Spark world as RDDs). 
These are flows whose results are partitioned and cached across the cluster, ready to be re-used again and again. 
This is particularly useful if you have an intermediary result set that you wish to query multiple times. 

In this case, you now persist the first few lines of the computation 
(which involves downloading the data from source and parsing with the CSV Type Provider), 
ready to be used for any number of strongly-typed queries we might have: –

*)

// download data, convert to provided type and partition across nodes in-memory only
let persistedHousePricesTask =
    sources
    |> CloudFlow.OfHttpFileByLine 
    |> CloudFlow.collect HousePrices.ParseRows
    |> CloudFlow.persist StorageLevel.Memory
    |> cluster.CreateProcess

(** Now observe progress: *)

persistedHousePricesTask.ShowInfo()
cluster.ShowWorkers()

(** Now wait for the results: *)

let persistedHousePrices = persistedHousePricesTask.Result

(** 

The input file will have been partitioned depending on the number of workers in your cluster.
The partitions are already assigned to different workers.
With the results persisted on the nodes, we can use them again and again.

First, get the total number of entries across the partitioned, persisted result:

*)

let count =
    persistedHousePrices
    |> CloudFlow.length
    |> cluster.Run

(** 
Next, get the first 100 entries:

*)

let first100 =
    persistedHousePrices
    |> CloudFlow.take 100
    |> CloudFlow.toArray
    |> cluster.Run


(** Next, get the average house price by year/month. *)

let pricesByMonthTask =
    persistedHousePrices
    |> CloudFlow.averageByKey 
          (fun row -> (row.DateOfTransfer.Year, row.DateOfTransfer.Month)) 
          (fun row -> float row.Price)
    |> CloudFlow.toArray
    |> cluster.CreateProcess

(** Make a chart of the results: *)

pricesByMonthTask.ShowInfo()
pricesByMonthTask.Result

let pricesByMonth = pricesByMonthTask.Result

pricesByMonth |> chartPrices



(** Next, get the average prices per street. CloudFlow.cache is the same as persiting to memory. *)

let averagePricesTask =
    persistedHousePrices
    |> CloudFlow.averageByKey
          (fun row -> (row.TownCity, row.Street))
          (fun row -> float row.Price)
    |> CloudFlow.cache
    |> cluster.CreateProcess

averagePricesTask.ShowInfo()

let averagePrices = averagePricesTask.Result

(** Next, use the cached results to get the most expensive city and street. *)

let mostExpensiveTask =
    averagePrices
    |> CloudFlow.sortByDescending snd 100
    |> CloudFlow.toArray
    |> cluster.CreateProcess

mostExpensiveTask.ShowInfo()
mostExpensiveTask.Result



(** Next, use the cached results to also get the least expensive city and street. *)
let leastExpensiveTask =
    averagePrices
    |> CloudFlow.sortBy snd 100
    |> CloudFlow.toArray
    |> cluster.CreateProcess

leastExpensiveTask.ShowInfo()
leastExpensiveTask.Result


(** Next, get the percentage of new builds by county: *)
let newBuildsByCountyTask =
    persistedHousePrices
    |> CloudFlow.averageByKey
          (fun row -> row.County)
          (fun row -> if row.NewBuild = "Y" then 1.0 else 0.0)
    |> CloudFlow.sortByDescending snd 100
    |> CloudFlow.toArray
    |> cluster.CreateProcess

newBuildsByCountyTask.ShowInfo()
newBuildsByCountyTask.Result


(** Make a chart of the results: *)

Chart.Column newBuildsByCountyTask.Result |> Chart.Show

(**

And so on.

So notice that the first query takes 45 seconds to execute,
which involves downloading the data and parsing it via the CSV type provider. 
Once we’ve done that, we persist it across the cluster in memory – 
then we can re-use that persisted flow in all subsequent queries, each of which just takes a few seconds to run.

*)