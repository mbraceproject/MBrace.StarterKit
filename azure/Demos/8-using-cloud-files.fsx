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
 This tutorial illustrates creating and using cloud files, and then processing them using cloud streams.
 
 Before running, edit credentials.fsx to enter your connection strings.
**)

// First connect to the cluster
let cluster = Runtime.GetHandle(config)

cluster.ShowProcesses()
cluster.ShowWorkers()

// Here's some data
let linesOfFile = ["Hello World"; "How are you" ] 

// Upload the data to a cloud file (held in blob storage). A fresh name is generated 
// for the could file.
let anonCloudFile = linesOfFile |> CloudFile.WriteAllLines |> cluster.Run

// Run a cloud job which reads all the lines of a cloud file:
let numberOfLinesInFile = 
    cloud { let! data = CloudFile.ReadAllLines anonCloudFile
            return data.Length }
    |> cluster.Run

// Get all the directories in the cloud file system
let directories = cluster.DefaultStoreClient.FileStore.Directory.Enumerate()

// Create a directory in the cloud file system
let dp = cluster.DefaultStoreClient.FileStore.Directory.Create()

// Upload data to a cloud file (held in blob storage) where we give the cloud file a name.
let namedCloudFile = 
    cloud { 
        let lines = [for i in 0 .. 1000 -> "Item " + string i + ", " + string (i * 100) ] 
        let fileName = dp.Path + "/file1"
        do! CloudFile.Delete(fileName) 
        let! file = CloudFile.WriteAllLines(lines, path = fileName) 
        return file
    } |> cluster.Run

// Access the cloud file as part of a cloud job
let numberOfLinesInNamedFile = 
    cloud { let! data = CloudFile.ReadAllLines namedCloudFile 
            return data.Length }
    |> cluster.Run

cluster.ShowLogs(240.0)

(** 

Now we generate a collection of cloud files and process them using cloud streams.

**)

// Generate 100 cloud files in the cloud storage
let namedCloudFilesJob = 
    [ for i in 1 .. 100 ->
        // Note that we generate the contents of the files in the cloud - this cloud
        // computation below only captures and sends an integer.
        cloud { let lines = [for j in 1 .. 100 -> "File " + string i + ", Item " + string (i * 100 + j) + ", " + string (j + i * 100) ] 
                let nm = dp.Path + "/file" + string i
                do! CloudFile.Delete(path=nm) 
                let! file = CloudFile.WriteAllLines(lines,path=nm) 
                return file } ]
   |> Cloud.Parallel 
   |> cluster.CreateProcess

// Check progress
namedCloudFilesJob.ShowInfo()

// Get the result
let namedCloudFiles = namedCloudFilesJob.AwaitResult()

// Compute 
let sumOfLengthsOfLinesJob =
    namedCloudFiles 
    |> CloudStream.ofCloudFiles CloudFileReader.ReadAllLines
    |> CloudStream.map (fun lines -> lines.Length)
    |> CloudStream.sum
    |> cluster.CreateProcess


// Check progress
sumOfLengthsOfLinesJob.ShowInfo()

// Get the result
let sumOfLengthsOfLines = sumOfLengthsOfLinesJob.AwaitResult()


