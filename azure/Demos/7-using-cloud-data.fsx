#load "credentials.fsx"

open System
open System.IO
open MBrace
open MBrace.Azure
open MBrace.Azure.Client
open MBrace.Azure.Runtime
open MBrace.Streams
open MBrace.Workflows
open Nessos.Streams

(**
 This tutorial illustrates uploading data to Azure Blob Storage using CloudRef and CloudArray and then using the data.
 
 Before running, edit credentials.fsx to enter your connection strings.
**)

// First connect to the cluster
let cluster = Runtime.GetHandle(config)
 
// Here's some data
let data = "Some data" 

// Upload the data to blob storage and return a handle to the stored data
let cloudData = data |> CloudCell.New |> cluster.Run

// Run a cloud job which reads the blob and processes the data
let lengthOfData = 
    cloud { let! data = CloudCell.Read cloudData 
            return data.Length }
    |> cluster.Run


(**
 Next we upload an array of data (each an array of tuples) as a CloudArray
 
**)

// Here is the data we're going to upload, it's a 500K long array of tuples
let vectorOfData = [| for i in 0 .. 1000 do for j in 0 .. i do yield (i,j) |]

// Upload it as a partitioned CloudArray, 100000 bytes/chunk
let cloudVector = CloudVector.New(vectorOfData,100000L) |> cluster.Run


// Check the partition count
cloudVector.PartitionCount


// Now process the cloud array
let lengthsJob = 
    cloudVector
    |> CloudStream.ofCloudVector
    |> CloudStream.map (fun (a,b) -> a+b)
    |> CloudStream.sum
    |> cluster.CreateProcess


// Check progress
lengthsJob.ShowInfo()

// Check progress
lengthsJob.Completed

// Acccess the result
let lengths =  lengthsJob.AwaitResult()

// Now process the cloud array again, using CloudStream.
// We process each element of the cloud array (each of which is itself an array).
// We then sort the results (500K elements!) and take the top 10 elements
let sumAndSortJob = 
    cloudVector
    |> CloudStream.ofCloudVector
    |> CloudStream.sortBy (fun (i,j) -> i * 10000 + j) 100
    |> CloudStream.toArray
    |> cluster.CreateProcess


// Check progress
sumAndSortJob.ShowInfo()

// Check progress
sumAndSortJob.Completed

// Acccess the result
let sumAndSort = sumAndSortJob.AwaitResult()


