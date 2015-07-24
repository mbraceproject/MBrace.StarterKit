(*** hide ***)
#load "credentials.fsx"

open System
open System.IO
open MBrace.Core
open MBrace.Store
open MBrace.Azure
open MBrace.Azure.Client
open MBrace.Flow

(**
# Creating and Using Cloud Files

 This tutorial illustrates creating and using cloud files, and then processing them using cloud streams.
 
 Before running, edit credentials.fsx to enter your connection strings.
*)

(** First you connect to the cluster: *)
let cluster = Runtime.GetHandle(config)

cluster.ShowProcesses()
cluster.ShowWorkers()

(** Here's some data that simulates a log file for user click events: *)
let linesOfFile = 
    [ for i in 1 .. 1000 do 
         let time = DateTime.Now.Date.AddSeconds(float i)
         let text = sprintf "click user%d %s" (i%10) (time.ToString())
         yield text ]

(** Upload the data to a cloud file (held in blob storage). A fresh name is generated for the could file. *) 
let anonCloudFile = 
     cloud { 
         let! path = CloudPath.GetRandomFileName()
         let! file = CloudFile.WriteAllLines(path, linesOfFile)
         return file 
     }
     |> cluster.Run

(** Run a cloud job which reads all the lines of a cloud file: *) 
let numberOfLinesInFile = 
    cloud { 
        let! data = CloudFile.ReadAllLines anonCloudFile.Path
        return data.Length 
    }
    |> cluster.Run

(** Get the default directory of the store client: *)
let defaultDirectory = CloudPath.DefaultDirectory |> cluster.RunLocally

(** Enumerate all subdirectories in the store client: *) 
cluster.StoreClient.Directory.Enumerate(defaultDirectory)

(** Create a directory in the cloud file system: *)
let directory = cluster.StoreClient.Path.GetRandomDirectoryName()
let freshDirectory = cluster.StoreClient.Directory.Create(directory)

(** Upload data to a cloud file (held in blob storage) where we give the cloud file a name. *) 
let namedCloudFile = 
    cloud { 
        let fileName = freshDirectory.Path + "/file1"
        do! CloudFile.Delete(fileName)
        let! file = CloudFile.WriteAllLines(fileName, linesOfFile)
        return file
    } 
    |> cluster.Run

(** Read the named cloud file as part of a cloud job: *)
let numberOfLinesInNamedFile = 
    cloud { 
        let! data = CloudFile.ReadAllLines namedCloudFile.Path
        return data.Length 
    }
    |> cluster.Run

cluster.ShowLogs(240.0)

(** 

Now we generate a collection of cloud files and process them using cloud streams.

*)

let namedCloudFilesJob = 
    [ for i in 1 .. 100 ->
        // Note that we generate the contents of the files in the cloud - this cloud
        // computation below only captures and sends an integer.
        cloud { 
            let lines = [for j in 1 .. 100 -> "File " + string i + ", Item " + string (i * 100 + j) + ", " + string (j + i * 100) ] 
            let nm = freshDirectory.Path + "/file" + string i
            do! CloudFile.Delete(path=nm)
            let! file = CloudFile.WriteAllLines(nm, lines)
            return file 
        } ]
   |> Cloud.Parallel 
   |> cluster.CreateProcess

// Check progress
namedCloudFilesJob.ShowInfo()

// Get the result
let namedCloudFiles = namedCloudFilesJob.AwaitResult()

(** A collection of cloud files can be used as input to a cloud
parallel data flow. This is a very powerful feature. *)
let sumOfLengthsOfLinesJob =
    namedCloudFiles
    |> Array.map (fun f -> f.Path)
    |> CloudFlow.OfCloudFilesByLine
    |> CloudFlow.map (fun lines -> lines.Length)
    |> CloudFlow.sum
    |> cluster.CreateProcess

// Check progress
sumOfLengthsOfLinesJob.ShowInfo()

// Get the result
let sumOfLengthsOfLines = sumOfLengthsOfLinesJob.AwaitResult()

(** In this tutorial, you've learned how to use cloud files
including as partitioned inputs into CloudFlow programming.
Continue with further samples to learn more about the
MBrace programming model.  *)

