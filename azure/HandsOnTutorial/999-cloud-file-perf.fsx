#load "credentials.fsx"

open System
open System.IO
open MBrace.Core
open MBrace.Store
open MBrace.Azure
open MBrace.Azure.Client
open MBrace.Flow

(**
 This script tests cloud file perf
 
 Before running, edit credentials.fsx to enter your connection strings.
**)

(** First you connect to the cluster: *)
let cluster = Runtime.GetHandle(config)

// Create a directory in the cloud file system
let dp = cluster.StoreClient.Directory.Create("perf-files")

//--------------------------------------------------------------------
// Stress test some data storage

let timeSizes sizes f = 
 [ for sz in sizes do 
    printfn "starting size %d" sz
    let job = 
        cloud {
          let! res = f sz
          return res
        }
        |> cluster.CreateProcess 
    printfn "awaiting result for size %d" sz
    let res = job.AwaitResult() 
    printfn "size %d took %fs" sz job.ExecutionTime.TotalSeconds
    yield (sz, job.ExecutionTime.TotalSeconds), res ]
    |> List.unzip



//  [(1, 1.0114461); (10, 1.2006469); (100, 3.8053827)]
//  [(1, 1.0706926); (10, 1.3171087); (100, 3.9861948)]
let lineWritePerf, bigCloudTextFiles = 
    timeSizes [ 1; 10; 100 ] <| fun sz -> 
        cloud { let lines = [| for i in 0 .. 10000 * sz-> "Some text that takes about one hundred bytes to store in default encoding if you look you can check" |] 
                // This delete is needed because of https://github.com/mbraceproject/MBrace.Azure/issues/21
                do! CloudFile.Delete(path=dp.Path + sprintf "/big-lines-%d" sz) 
                let! file = CloudFile.WriteAllLines(path=dp.Path + sprintf "/big-lines-%d" sz, lines = lines)  
                return file }

// [(1, 0.6813008); (10, 1.200415); (100, 6.4491432)]
// [(1, 1.133324); (10, 1.5016807); (100, 6.7401698)]
let textWritePerf, _ = 
    timeSizes [ 1; 10; 100 ] <| fun sz -> 
        cloud { let text = System.String(' ', sz * 1024 * 1024)
                // This delete is needed because of https://github.com/mbraceproject/MBrace.Azure/issues/21
                do! CloudFile.Delete(path=dp.Path + sprintf "/big-text-%d" sz) 
                let! file = CloudFile.WriteAllText(path=dp.Path + sprintf "/big-text-%d" sz, text = text)  
                return file }

// #1   [(1, 0.5426625); (10, 2.0498235); (100, 21.1039728)]
// #2   [(1, 0.8444004); (10, 1.9827996); (100, 21.1224367)]
// #3   [(1, 0.6401685); (10, 1.8751011); (100, 16.3687715); (1000, 219.2221633)]
// #4   [(1, 0.7645828); (10, 2.0262016); (100, 16.4836501); (1000, 234.8571303)]
let bytesWritePerf, bigCloudByteFiles = 
    timeSizes [ 1; 10; 100; 1000 ] <| fun sz -> 
        cloud { let data = [| for i in 0 .. sz * 1024 * 1024 -> byte i |] 
                // This delete is needed because of https://github.com/mbraceproject/MBrace.Azure/issues/21
                do! CloudFile.Delete(path=dp.Path + sprintf "/big-bytes-%d" sz) 
                let! file = CloudFile.WriteAllBytes (path=dp.Path + sprintf "/big-bytes-%d" sz, buffer = data)  
                return file }

// #1   [(1, 3.1591611); (10, 0.5312207); (100, 3.0732776)]
// #2   [(1, 0.2813198); (10, 0.4259485); (100, 2.6784925)]
// #3   [(1, 0.8718545); (10, 1.1114111); (100, 2.6215714)]
let lineReadPerf, lineReadResults = 
    timeSizes [ 1; 10; 100 ] <| fun sz -> 
        cloud { let cloudFile =  CloudFile(dp.Path + sprintf "/big-lines-%d" sz) 
                let! lines =  CloudFile.ReadAllLines(cloudFile.Path)   
                return lines.Length }

// #1 [(1, 0.3036431); (10, 0.397323); (100, 8.2052502)]
// #2   [(1, 0.3057246); (10, 0.3347746); (100, 1.7994133)]
// #3   [(1, 0.302497); (10, 0.3630366); (100, 1.8345874)]
// =  approx 100MB/sec
let textReadPerf, textReadResults = 
    timeSizes [ 1; 10; 100 ] <| fun sz -> 
        cloud { let cloudFile =  CloudFile(dp.Path + sprintf "/big-text-%d" sz) 
                let! text =  CloudFile.ReadAllText(cloudFile.Path)   
                return text.Length }


// #1 -   [(1, 0.3307201); (10, 0.5625038); (100, 5.9124926)]
// #2 -   [(1, 0.367018); (10, 0.5394005); (100, 0.7989355)]
// #3 -   [(1, 0.3044898); (10, 0.3846313); (100, 1.1439518)]
// #4 -   [(1, 0.6358480); (10, 0.5383680); (100, 1.6542884); (1000, 5.7805793)]
// #5 -   [(1, 0.5175206); (10, 0.3935077); (100, 1.4001587); (1000, 10.5389589)]
let bytesReadPerf, bytesReadResults = 
    timeSizes [ 1; 10; 100; 1000 ] <| fun sz -> 
        cloud { let cloudFile = CloudFile(dp.Path + sprintf "/big-bytes-%d" sz)
                let! bytes =  CloudFile.ReadAllBytes(cloudFile.Path)   
                return bytes.Length }

