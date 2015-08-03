(*** hide ***)
#load "credentials.fsx"

open System
open System.IO
open MBrace.Core
open MBrace.Store
open MBrace.Azure
open MBrace.Azure.Client

(**
This tutorial illustrates how to upload local files to Azure blob storage and 
process the files in the cloud using MBrace.

Before running, edit credentials.fsx to enter your connection strings.
*)

(** First you connect to the cluster: *)
let cluster = Runtime.GetHandle(config)

cluster.ShowProcesses()
cluster.ShowWorkers()


(** Now you define functions to create and remove temp files. *)
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


(** Next, you upload the created file to the tmp container in Azure blob storage. The tmp container
will be created if it does not exist. Note the use of the local {...} expression and the cluster.RunLocally method:  
the uploading has to be run locally because it accesses a local path. *)
let cFile = 
    local {
        return! CloudFile.Upload(tmpFile, sprintf "tmp/%s" (Path.GetFileName tmpFile))     
    }    
    |> cluster.RunLocally

(** After uploading the file, you remove the local file. *)
DeleteTempFile tmpFile

(** At last, you count the lines of the uploaded file in the MBrace cluster. This cloud expression runs in the MBrace cluster. *)
let lines = 
    cloud {
        let! lines = CloudFile.ReadAllLines cFile.Path 
        return lines.Length
    } 
    |> cluster.Run

(** In this tutorial, you've learnt how to upload local files into Azure blob storage and then
process the uploaded files in the MBrace cluster. *)
