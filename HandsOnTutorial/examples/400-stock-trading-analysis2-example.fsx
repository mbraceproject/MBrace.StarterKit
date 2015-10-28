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
        //for i in 1..20 do
            let md = SimulateMarketSlice stockInfo
            let! mdc = CloudValue.New md
            let mdp = { Items = mdc } 
            tradingDataQueue.Enqueue mdp
            do! Cloud.Sleep 3000
        let result = tradingDataQueue.GetCount()
        return result
    } 
    
let simulationTask = SimulateMarket stockInfo |> cluster.CreateProcess   
 

//let itemCount = tradingDataQueue.GetCount()

// The data structure used to calculate running mean of ask or bid volume.
type MeanAskOrBidVolume = {
    Count: double  // The number of elements so far to calculate mean.
    Mean: double   // Previous mean value.
}

// The cloud dictionary to store running means of stock traded volumes.
// Keys of the dictionary are stock symbols, values are record representing
// the running mean of the ask and bid volumes.
let dict = 
    cloud {
        let! dict = CloudDictionary.New<MeanAskOrBidVolume>();
        return dict
    } |> cluster.Run

let AnalyzeMarketData = 
    cloud {        
        //for i in 1..10 do
        while true do
            let data_group = tradingDataQueue.Dequeue()
            let data_groups = [| data_group |]                    
            if data_groups.Length > 0 then                            
                let! stocksWithLargeAskOrBid = 
                    data_groups
                    |> CloudFlow.OfArray
                    |> CloudFlow.collect(fun p -> p.Items.Value)
                    |> CloudFlow.filter(fun p -> 
                        let symbol = p.Symbol
                        // Calculate running mean of ask and bid volumes for a stock.
                        let askAndBid = Array.concat([|p.Asks; p.Bids|])
                        let askAndBidSum = Array.sum(askAndBid)
                        let askAndBidLen = double askAndBid.Length
                        let oldMean = dict.TryFind symbol         
                        
                        let (newMean, newCount) = 
                            match oldMean with
                            | Some(oldMean) ->  ((oldMean.Count * oldMean.Mean + askAndBidSum) /  (oldMean.Count + askAndBidLen), oldMean.Count + askAndBidLen)
                            | None -> (askAndBidSum / askAndBidLen, askAndBidLen)  

                        // Store new running mean in the cloud dictionary.
                        dict.AddOrUpdate(symbol, function _ -> { Mean=newMean; Count=newCount; })  |> ignore                      
                            
                        // A stock is signaled if it has ask or bid whose volume is larger than 1.2 times of its running mean.
                        let largeAsk = p.Asks |> Array.filter(fun v -> v > 1.5 * newMean) |> Seq.length
                        let largeBid = p.Bids|> Array.filter(fun v -> v > 1.5 * newMean) |> Seq.length
                        largeAsk + largeBid > 0                      
                      )
                    |> CloudFlow.toArray

                if stocksWithLargeAskOrBid.Length > 0 then
                    let! cValue = CloudValue.New stocksWithLargeAskOrBid
                    let analysisResult = { Items = cValue }  
                    resultQueue.Enqueue analysisResult                
            else
                do! Cloud.Sleep(1000)
    }

let analysisTask = AnalyzeMarketData |> cluster.CreateProcess
