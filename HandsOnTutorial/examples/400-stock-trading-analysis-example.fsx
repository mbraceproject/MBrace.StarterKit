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
*)

// Type that represents stock trading data.
type StockInfo = {
    Symbol: string
    Price: double
    Volume: double
}

// Setup CSV type provider.
[<Literal>]
let stockDataPath = __SOURCE_DIRECTORY__ + "/../../data/stock-data.csv"
type Stocks = CsvProvider<stockDataPath>

// The following lines read data from Azure blog storage.
// If you run with a real Azure cluster, use the following lines.
//let fileSystem = cluster.Store.CloudFileSystem
//let ReadAllText path = fileSystem.File.ReadAllText path
//let text = ReadAllText "/stock-data/stock-data.csv"
//let data = Stocks.Load(new StringReader(text))

// This line reads data from local file system.
// If you run with the Spian cluster, use the following line.
let data = Stocks.Load(stockDataPath)

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

// A group of many data packages, this is the element that we are going to put into the queue.
// Group many packages together will reduce the number of Azure APIs (500 per second quota).
// Need to respect the 64KB queue message side from Azure.
type MarketDataGroup = {
    Items : CloudValue<MarketDataPackage[]>
}


// Generate simulated market data at a one timestamp.
let SimulateMarketSlice (stockInfo : seq<StockInfo>) =
    let r = Random()
    seq {
        for s in stockInfo do
            let symbol = s.Symbol
            let newPrice = s.Price *  (1.0 + r.NextDouble()/5.0 - 0.1)
            let newVolume = s.Volume
            let asks = [| float (r.Next(500, 1500)) |]
            let bids = [| float (r.Next(500, 1500)) |]
            yield { Symbol=symbol; Price=newPrice; Volume=newVolume; Asks=asks; Bids=bids; } } 
    |> Seq.toArray


// The queue which stores stock trading data.
let tradingDataQueue = CloudQueue.New<MarketDataGroup>() |> cluster.Run

// The queue which stores analysis results.
let resultQueue = CloudQueue.New<MarketDataGroup>() |> cluster.Run



// Generate simulated market data, for the moment, generate 10 slices.
// Wait 3 seconds between two slices.
let SimulateMarket stockInfo =
    cloud {                
        while true do
        //for i in 1..10 do
            let md = SimulateMarketSlice stockInfo
            let! mdc = CloudValue.New md
            let mdp = { Items = mdc } 
            tradingDataQueue.Enqueue mdp
            do! Cloud.Sleep 3000
        let result = tradingDataQueue.GetCount()
        return result
    } 
    
let simulationTask = SimulateMarket stockInfo |> cluster.CreateProcess   
 

let LargeBidVolume = 1000.0
let LargeAskVolume = 1000.0

// Does a market data have large ask or bid?
let HasLargeAskOrBid(md : MarketDataPackage) = 
    let largeAsk = md.Asks |> Array.filter(fun v -> v > LargeBidVolume) |> Seq.length
    let largeBid = md.Bids |> Array.filter(fun v -> v > LargeAskVolume) |> Seq.length
    largeAsk + largeBid > 0

// The task to process simulated stock trading data and generates
// signals when a stock with large ask or bid volume is detected.
let AnalyzeMarketData = 
    cloud {
        while true do
            let data_group = tradingDataQueue.Dequeue()
            let data_groups = [| data_group |]                    
            if data_groups.Length > 0 then
                // The task is simple now, just get the market data which has large asks or bids.
                let! stocksWithLargeAskOrBid = 
                    data_groups
                    |> CloudFlow.OfArray
                    |> CloudFlow.collect(fun p -> p.Items.Value)
                    |> CloudFlow.filter(fun md ->  HasLargeAskOrBid(md))
                    |> CloudFlow.toArray

                let! cValue = CloudValue.New stocksWithLargeAskOrBid
                let analysisResult = { Items = cValue }  
                resultQueue.Enqueue analysisResult
            else
                do! Cloud.Sleep(1000)
    }

let analysisTask = AnalyzeMarketData |> cluster.CreateProcess
