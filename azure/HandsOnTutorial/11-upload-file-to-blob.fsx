(*** hide ***)
#load "credentials.fsx"

open System
open System.IO
open MBrace.Core
open MBrace.Store
open MBrace.Azure
open MBrace.Azure.Client
open MBrace.Flow

open Microsoft.WindowsAzure
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Auth
open Microsoft.WindowsAzure.Storage.Blob

(**
This tutorial illustrates how to upload local files to Azure blob storage and 
process the files in the cloud using MBrace.

Before running, edit credentials.fsx to enter your connection strings.
*)

(** First you connect to the cluster: *)
let cluster = Runtime.GetHandle(config)

cluster.ShowProcesses()
cluster.ShowWorkers()


(** Create a tmp file locally, wrote some random content, and return the file path. *)
let CreateTempFile() =    
    let tmpFile = Path.GetTempFileName()
    use sw = new StreamWriter(tmpFile)
    for i in 1..100 do 
        sw.WriteLine i
    tmpFile

(** Delete a file given its path *)
let DeleteTempFile path =
    File.Delete path


(** Create a local temp file. *)
let tmpFile = CreateTempFile()

// Using Azure SDK to create a tmp container in Azure blob.
let containerName = "tmp"
let storageAccount = CloudStorageAccount.Parse myStorageConnectionString
let blobClient = storageAccount.CreateCloudBlobClient()
let container = blobClient.GetContainerReference(containerName)
container.CreateIfNotExists()


(** 
Upload the temp file to the tmp container. Note that this expression 
has to be run locally using cluster.RunLocally because it accesses local paths.  
*)
let cFile = 
    CloudFile.Upload(tmpFile, sprintf "%s/%s" containerName (Path.GetFileName tmpFile)) 
    |> cluster.RunLocally

(** Delete the local temp file *)
DeleteTempFile tmpFile

(** Cound the lines of the uploaded file in the MBrace cluster. *)
let lines = 
    CloudFile.ReadAllLines cFile.Path 
    |> cluster.Run
    |> Array.length









