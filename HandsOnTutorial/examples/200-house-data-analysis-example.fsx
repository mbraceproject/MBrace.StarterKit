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

# Using Data Parallel Cloud Flows to Analyze Historical Event Data

In this example, you learn how to use data parallel cloud flows with historical event data at scale.
The data is drawn directly from open government data on the internet.  This sample has been adapted
from Isaac Abraham's [blog](https://cockneycoder.wordpress.com/2015/10/20/mbrace-cloudflows-and-fsharp-data-a-perfect-match/).

You start by using FSharp.Data and its CSV Type Provider. 
Usually the type provider can infer all data types and columns but in this case the file does 
not include headers, so we’ll supply them ourselves. 
You use a local version of the CSV file which contains a subset of the data 
(the live dataset even for a single month is > 10MB)

*)

type HousePrices = CsvProvider< @"../../data/SampleHousePrices.csv", HasHeaders = true>

(**

With that, you have a strongly-typed way to parse CSV data. 

Here is the input data.  (Each of these files is ~70MB but can take a significant amount of time to download
due to possible rate-limiting from the server).
*)

let smallSources = 
  [ "http://publicdata.landregistry.gov.uk/market-trend-data/price-paid-data/b/pp-2012-part1.csv" ]

(* For larger data you can add more years: *)

let bigSources = 
  [ "http://publicdata.landregistry.gov.uk/market-trend-data/price-paid-data/a/pp-2012.csv"
    "http://publicdata.landregistry.gov.uk/market-trend-data/price-paid-data/a/pp-2013.csv"
    "http://publicdata.landregistry.gov.uk/market-trend-data/price-paid-data/a/pp-2014.csv"
    "http://publicdata.landregistry.gov.uk/market-trend-data/price-paid-data/a/pp-2015.csv"  ]

(* For testing you can uses micro data: *)

let tinySources = 
  [ "https://raw.githubusercontent.com/mbraceproject/MBrace.StarterKit/master/data/" 
    + "SampleHousePriceFile.csv" ]

(**
Now, stream the data source from the original web location and 
across the cluster, then convert the raw text to our CSV provided type.
Entries are grouped by month and the average price for each month is computed.

*)
//let sources = tinySources
let sources = smallSources
//let sources = bigSources

let pricesTask =
    sources
    |> CloudFlow.OfHttpFileByLine
    |> CloudFlow.collect HousePrices.ParseRows
    |> CloudFlow.averageByKey 
            (fun row -> row.DateOfTransfer.Year, row.DateOfTransfer.Month) 
            (fun row -> float row.Price)
    |> CloudFlow.sortBy fst 100
    |> CloudFlow.toArray
    |> cluster.CreateProcess


(** 

A *CloudFlow* is an MBrace primitive which allows a distributed set of transformations to be chained together.
A CloudFlow pipeline is partitioned across the cluster, making full use of resources available:
only when the pipelines are completed in each partition are they aggregated together again.

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

(** 
Now wait for the results. 
*)

pricesTask.ShowInfo()
cluster.ShowWorkers()

let prices = pricesTask.Result

(**

Now that you have a summary array of year, month and price data, you can chart the data.

*)

let formatYearMonth (year,month) = 
    sprintf "%s" (DateTime(year, month, 1).ToString("yyyy-MMM"))

let chartPrices prices = 
    prices
    |> Seq.map(fun (ym, price) -> formatYearMonth ym, price)
    |> Chart.Line
    |> Chart.WithOptions(Options(curveType = "function"))
    |> Chart.Show

chartPrices prices
(**

![Price over time (2012 subset of data)](../img/house-prices-chart-1.png)

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
    |> CloudFlow.sortBy fst 100
    |> CloudFlow.toArray
    |> cluster.CreateProcess

(** Make a chart of the results. This will be the same chart as before, but based on persisted results. *)

pricesByMonthTask.ShowInfo()
pricesByMonthTask.Result

let pricesByMonth = pricesByMonthTask.Result

pricesByMonth |> chartPrices



(** 
Next, get the average prices per street. This takes a fair while since there are a lot of streets.
We persist the results: CloudFlow.cache is the same as persiting to memory. 
*)

let averagePricesTask =
    persistedHousePrices
    |> CloudFlow.averageByKey
          (fun row -> (row.TownCity, row.District, row.Street))
          (fun row -> float row.Price)
    |> CloudFlow.cache
    |> cluster.CreateProcess

averagePricesTask.ShowInfo()

let averagePrices = averagePricesTask.Result

(** Next, use the cached results to get the most expensive city and street. *)

let mostExpensive =
    averagePrices
    |> CloudFlow.sortByDescending snd 100
    |> CloudFlow.toArray
    |> cluster.Run


(** Next, use the cached results to also get the least expensive city and street. *)
let leastExpensive =
    averagePrices
    |> CloudFlow.sortBy snd 100
    |> CloudFlow.toArray
    |> cluster.Run


(** Count the sales by city: *)

let purchasesByCity =
    persistedHousePrices
    |> CloudFlow.countBy (fun row -> row.TownCity)
    |> CloudFlow.sortByDescending snd 1500
    |> CloudFlow.toArray
    |> cluster.Run

Chart.Pie purchasesByCity |> Chart.Show


(** 
![Count by city](../img/house-prices-by-city.png)

And so on.

So notice that the first query takes 45 seconds to execute,
which involves downloading the data and parsing it via the CSV type provider. 
Once we’ve done that, we persist it across the cluster in memory – 
then we can re-use that persisted flow in all subsequent queries, each of which just takes a few seconds to run.


### Finding the Current Prices of Monopoly Streets

We all know and love the game [Monopoly](https://en.wikipedia.org/wiki/Go_to_jail).
For those who grew up in the United Kingdom or Australia, you probably played
using the London street names and prices where buying Mayfair cost £400.  But how do
prices today look, and which streets are now the most expensive?

Next, you get the list of all streets on the Monopoly board using the HTML type provider
over [this web page](http://www.jdawiseman.com/papers/trivia/monopoly-rents.html).
This is a type provider in FSharp.Data that helps you crack the content of HTML tables.
*)

type MonopolyTable = 
    HtmlProvider<"http://www.jdawiseman.com/papers/trivia/monopoly-rents.html">

let monopolyPage = MonopolyTable.GetSample()

(**
This page contains a particular table with all the property names for the UK edition of Monopoly:
*)
let data = monopolyPage.Tables.Table2.Rows 

(** 

Giving:

    [("Property", "Cost", "M’tg", "Site", "1 hse", "2 hses", "3 hses", "4 hses","Hotel");
     ("Old Kent Road", "60", "30", "2", "10", "30", "90", "160", "250");
     ...
     ("Park Lane", "350", "175", "35", "175", "500", "1100", "1300", "1500");
     ("Mayfair", "400", "200", "50", "200", "600", "1400", "1700", "2000")]

*)

(**
Next you put the names into a set, converting them to lower case as you go:
*)

let monopolyStreets = 
        [ for p in data do 
             let streetName = p.Column1.ToLower()
             // Strip off the header 
             if streetName <> "Property" then 
                 yield streetName
          // Yield one random street in Mayfair
          yield "Grosvenor Street".ToLower()]
        |> set


(**
Next, you find the sales that correspond to Monopoly streets, again reusing your calculation
of average-prices on streets:
*)

let monopoly =
    averagePrices
    |> CloudFlow.filter (fun ((city, district, street),price) -> 
          city = "LONDON" && monopolyStreets.Contains(street.ToLower())) 
    |> CloudFlow.sortByDescending snd 100
    |> CloudFlow.toArray
    |> cluster.Run




(**

You're done! If you are using the large 4-year data set, you will see this:

    [|(("LONDON", "CITY OF WESTMINSTER", "TRAFALGAR SQUARE"), 5540000.0);
      (("LONDON", "CITY OF WESTMINSTER", "PICCADILLY"), 3500000.0);
      (("LONDON", "CITY OF WESTMINSTER", "THE STRAND"), 3100000.0);
      (("LONDON", "KENSINGTON AND CHELSEA", "MARLBOROUGH STREET"), 2975000.0);
      (("LONDON", "CITY OF WESTMINSTER", "PARK LANE"), 2465000.0);
      (("LONDON", "CITY OF WESTMINSTER", "OXFORD STREET"), 1803150.0);
      (("LONDON", "CITY OF WESTMINSTER", "WHITEHALL"), 1130000.0);
      (("LONDON", "CITY OF WESTMINSTER", "BOW STREET"), 1056428.571);
      (("LONDON", "CAMDEN", "EUSTON ROAD"), 1045781.25);
      (("LONDON", "CITY OF WESTMINSTER", "GROSVENOR STREET"), 717400.0);
      (("LONDON", "CITY OF WESTMINSTER", "PALL MALL"), 700000.0);
      (("LONDON", "CITY OF LONDON", "FLEET STREET"), 621111.1111);
      (("LONDON", "BRENT", "REGENT STREET"), 581421.4286);
      (("LONDON", "REDBRIDGE", "NORTHUMBERLAND AVENUE"), 549850.0);
      (("LONDON", "ISLINGTON", "PENTONVILLE ROAD"), 539376.4634);
      (("LONDON", "NEWHAM", "BOND STREET"), 360000.0);
      (("LONDON", "TOWER HAMLETS", "WHITECHAPEL ROAD"), 330071.4286);
      (("LONDON", "ENFIELD", "PARK LANE"), 290400.0);
      (("LONDON", "SOUTHWARK", "OLD KENT ROAD"), 266975.6622);
      (("LONDON", "HARINGEY", "PARK LANE"), 210770.8333);
      (("LONDON", "EALING", "BOND STREET"), 206000.0)|]
*)

(**

Some of these results are false: they are probably not the streets being referred
to in the Monopoly game!  But most are accurate.  You will see that the "red" set
does particularly well in the 21st century!!  Also, many of the "cheap" properties
are still at the lower end of the list, albeit at roughly 7,000x the price of the original
game!

## Summary

In this example, you've learned how to use data parallel cloud flows with historical event data
drawn directly from the internet.  By using F# type providers (FSharp.Data) plus a sample of the data 
you have given strong types to your information.  You then learned how to persist partial results
and to calculate averages and sums of groups of the data.

Continue with further samples to learn more about the MBrace programming model.  

> Note, you can use the above techniques from both scripts and compiled projects. To see the components referenced 
> by this script, see [ThespianCluster.fsx](../ThespianCluster.html) or [AzureCluster.fsx](../AzureCluster.html).

*)