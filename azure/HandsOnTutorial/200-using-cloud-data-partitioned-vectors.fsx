#load "credentials.fsx"
#r "MBrace.Flow.dll"

open System
open System.IO
open MBrace
open MBrace.Azure
open MBrace.Azure.Client
open MBrace.Workflows
open MBrace.Flow


(**
 This tutorial illustrates uploading data to Azure using 
 the partitioned data structure CloudVector, and then using the data.

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



// Here is the data we're going to upload, it's 1000 tuples (100MB)
let vectorOfData = 
     [| for i in 1 .. 1000 do 
          let text = sprintf "%d quick brown foxes jumped over %d lazy dogs\r\n" i i
          yield (i, String.replicate 100 text) |]


// Upload it as a partitioned CloudVector, 100000 bytes/chunk
let persistedVectorOfData = 
    cloud { let! cv = CloudVector.New(vectorOfData,100000L) 
            return cv }
    |> cluster.Run


// Check the partition count - it should be about 34 partitions
persistedVectorOfData.PartitionCount


// Now process the cloud vector. This is done using a data-parallel
// CloudFlow.  We do a word count on the text in the data.
let lengthsJob = 
    persistedVectorOfData
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
    persistedVectorOfData
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


