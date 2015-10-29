(*** hide ***)
#load "../ThespianCluster.fsx"
//#load "../AzureCluster.fsx"
#r "../../packages/FSharp.Data/lib/net40/FSharp.Data.dll"

open System
open System.IO
open MBrace.Core
open MBrace.Flow
open FSharp.Data

// Grab the MBrace cluster.
let cluster = Config.GetCluster() 

(**

# Creating an Incremental Stock Analysis 

This example shows how to create a stock trading simulator.

First, setup the CSV type provider to read a list of stocks in a strongly typed way:
*)

[<Literal>]
let stockDataPath = __SOURCE_DIRECTORY__ + "/../../data/stock-data.csv"
type Stocks = CsvProvider<stockDataPath>


(** Load the list of stocks. This is relatively small data, so we can read it locally. *)

let data = Stocks.Load(stockDataPath)

(**

Next, define a type that represents stock trading data:

*)

type StockInfo = {
    Symbol: string
    Price: double
    Volume: double
}
(** Next, you extract some essential information from the list of stocks. *)

let stockInfo = 
    [| for row in data.Rows do
        yield { Symbol=row.Symbol; Price=double row.Price; Volume=double row.Volume; }
    |] 

// Record for a single data package from the trading API.
type MarketDataPackage = {    
    Symbol : string
    Price : double
    Volume: double
    Asks: double[]
    Bids: double[]
}

(** 
Next, define a function to generate simulated market data at a one timestamp based 
on the input list of stocks and their average prices: 

*)


let SimulateMarketSlice (stockInfo : StockInfo[]) =
    let r = Random()
    [| for s in stockInfo -> 
         { Symbol = s.Symbol
           Price = s.Price *  (1.0 + r.NextDouble()/5.0 - 0.1)
           Volume = s.Volume
           Asks = [| float (r.Next(500, 1500)) |]
           Bids = [| float (r.Next(500, 1500)) |] 
         } 
    |]



(** Next, define the queue which stores incoming market trading data. 

We group many data packages together, write them to storage, and put them into the queue as one element.
Group many packages together will reduce the number of cloud I/O operations which is 
restricted by quota on most fabrics.  Additionally, the size of elements we can write to the queue
is also restricted, so we write a cloud value, and the queue holds a reference to this cloud value.
*)

type MarketDataGroup = CloudValue<MarketDataPackage[]>

let tradingDataQueue = CloudQueue.New<MarketDataGroup>() |> cluster.Run

(** Next, define the queue to store analysis results: *)

let resultQueue = CloudQueue.New<MarketDataGroup>() |> cluster.Run


(** 
Next, define a function to generate simulated market data and write it into the request queue:
Wait 3 seconds between two slices.
*)

let SimulateMarket stockInfo =
    cloud {                
        while true do
        //for i in 1..10 do
            let md = SimulateMarketSlice stockInfo
            let! mdc = CloudValue.New md
            tradingDataQueue.Enqueue mdc
            do! Cloud.Sleep 3000
    } 
    

(** 
Next, start the simulation task to generate market data into the request queue:
*)

let simulationTask = SimulateMarket stockInfo |> cluster.CreateProcess   
 

(** 
Next, you define a function to determine if market data has a large ask or bid volume:
*)

let LargeBidVolume = 1000.0
let LargeAskVolume = 1000.0

let HasLargeAskOrBid(md : MarketDataPackage) = 
    let largeAsk = md.Asks |> Array.filter(fun v -> v > LargeBidVolume) |> Seq.length
    let largeBid = md.Bids |> Array.filter(fun v -> v > LargeAskVolume) |> Seq.length
    largeAsk + largeBid > 0

(** 
Next, define the task to process simulated stock trading data and generate
signals when a stock with large ask or bid volume is detected.
*)

let AnalyzeMarketData = 
    cloud {
        while true do
            let dataGroup = tradingDataQueue.Dequeue()
            let dataGroups = [| dataGroup |]                    
            if dataGroups.Length > 0 then
                // The task is simple now, just get the market data which has large asks or bids.
                let! stocksWithLargeAskOrBid = 
                    dataGroups
                    |> CloudFlow.OfArray
                    |> CloudFlow.collect(fun p -> p.Value)
                    |> CloudFlow.filter(fun md ->  HasLargeAskOrBid(md))
                    |> CloudFlow.toArray

                let! analysisResult = CloudValue.New stocksWithLargeAskOrBid
                resultQueue.Enqueue analysisResult
            else
                do! Cloud.Sleep(1000)
    }

(** You now start the analysis task: *)

let analysisTask = AnalyzeMarketData |> cluster.CreateProcess

(** Next, get batches of results from the result queue: *)

resultQueue.DequeueBatch(10)

(** Finally, cancel the running simulation tasks: *)

simulationTask.Cancel()
analysisTask.Cancel()

(** And check that all tasks have completed on the cluster: *)
cluster.ShowProcesses()
cluster.ShowWorkers()


(** 
## Summary

In texample, you learned you to create a simulation running in the cloud.  The components
in the simulation take base data and write outputs to cloud queues. 
Continue with further samples to learn more about the MBrace programming model.  


> Note, you can use the above techniques from both scripts and compiled projects. To see the components referenced 
> by this script, see [ThespianCluster.fsx](../ThespianCluster.html) or [AzureCluster.fsx](../AzureCluster.html).
 *)


