#load "credentials.fsx"

open System
open System.IO
open MBrace
open MBrace.Azure
open MBrace.Azure.Client
open MBrace.Workflows
open MBrace.Flow


(**
 You now learn how to upload data to Azure Blob Storage using CloudCell.

 When using MBrace, data is implicitly uploaded if it is
 part of the closure of a cloud workflow - for example, if a value is
 referenced in a cloud { ... } block.  That data is a transient part of the 
 process specification.  This is often the most convenient way to get 
 small amounts (KB-MB) of data to the cloud: just use the data as part
 of a cloud workflow and run that work in the cloud.

 If you wish to _persist_ data in the cloud - for example, if it is too big
 to upload multiple times - then you can use one or more of the
 cloud data constructs that MBrace provides. 
 
 Note you can alternatively you use any existing cloud storage 
 APIs or SDKs you already have acces to. For example, if you wish you 
 can read/write using the .NET Azure storage SDKs directly rather than 
 using MBrace primitives.
 
 Before running, edit credentials.fsx to enter your connection strings.
**)

// First connect to the cluster
let cluster = Runtime.GetHandle(config)
 
// Here's some data (~1.0MB)
let data = 
    String.replicate 10000 "The quick brown fox jumped over the lazy dog\r\n" 


// Upload the data to blob storage and return a handle to the stored data
//
let persistedCloudData = 
    cloud { let! cell = CloudCell.New data 
            return cell }
    |> cluster.Run

// Run a cloud job which reads the blob and processes the data
let lengthOfData = 
    cloud { let! data = CloudCell.Read persistedCloudData 
            return data.Length }
    |> cluster.Run


(**
 Next we upload an array of data (each element a tuple) as a CloudArray
 
**)

// Here is the data we're going to upload, it's 1000 tuples (100MB)
let vectorOfData = 
     [| for i in 1 .. 1000 do 
          let text = sprintf "%d quick brown foxes jumped over %d lazy dogs\r\n" i i
          yield (i, String.replicate 100 text) |]


// Upload it as a partitioned CloudArray, 100000 bytes/chunk
let cloudVector = 
    cloud { let! cv = CloudVector.New(vectorOfData,100000L) 
            return cv }
    |> cluster.Run


// Check the partition count - it should be about 34 partitions
cloudVector.PartitionCount


// Now process the cloud array, doing a word count on the text in the data
let lengthsJob = 
    cloudVector
    |> CloudFlow.ofCloudVector
    |> CloudFlow.map (fun (i,text) -> 
          text.Split( [|' ';'\r';'\n' |],StringSplitOptions.RemoveEmptyEntries).Length)
    |> CloudFlow.sum
    |> cluster.CreateProcess


// Check progress
lengthsJob.ShowInfo()

// Check progress
lengthsJob.Completed

// Acccess the result (should be 900000 words)
let lengths =  lengthsJob.AwaitResult()

// Now process the cloud array again, using CloudFlow.
// We then sort the results and take the top 10 elements
let sortJob = 
    cloudVector
    |> CloudFlow.ofCloudVector
    |> CloudFlow.sortByDescending (fun (i,j) -> i + j.Length) 10
    |> CloudFlow.toArray
    |> cluster.CreateProcess


// Check progress
sortJob.ShowInfo()

// Check progress
sortJob.Completed

// Acccess the result
let sortResult = sortJob.AwaitResult()


