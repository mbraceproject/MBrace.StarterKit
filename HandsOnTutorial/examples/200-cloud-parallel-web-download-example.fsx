(*** hide ***)
#load "../ThespianCluster.fsx"
//#load "AzureCluster.fsx"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit AzureCluster.fsx to enter your connection strings.

open System
open System.IO
open MBrace.Core
open MBrace.Flow



(**
# Example: Cloud Parallel Web Downloader 

This example illustrates doing I/O tasks in parallel using the workers in the cluster
 
*)

// Cloud parallel url-downloader
open System.Net

let cluster = Config.GetCluster() 
let fs = cluster.Store.CloudFileSystem

let urls = 
    [| ("bing", "http://bing.com")
       ("yahoo", "http://yahoo.com")
       ("google", "http://google.com")
       ("msn", "http://msn.com") |]

(** Next define a cloud workflow to download a file and wave it into cloud storage. *)
let download (name: string, uri: string) = 
    cloud {
        let webClient = new WebClient()
        let! text = webClient.AsyncDownloadString(Uri(uri)) |> Cloud.OfAsync
        do! CloudFile.Delete(sprintf "pages/%s.html" name)
        let! file = CloudFile.WriteAllText(path = sprintf "pages/%s.html" name, text = text)
        return file
    }

let filesTask = 
    urls 
    |> Array.map download
    |> Cloud.Parallel
    |> cluster.CreateProcess

// Check on progress...
filesTask.ShowInfo()

// Get the result of the job
let files = filesTask.Result

// Read the files we just downloaded
let contentsOfFiles = 
    files
    |> Array.map (fun file ->
        cloud { let text = fs.File.ReadAllText(file.Path)
                return (file.Path, text.Length) })
    |> Cloud.Parallel
    |> cluster.Run


(** In this example, you've seen how cloud tasks can perform I/O to web data sources. 
Continue with further samples to learn more about the
MBrace programming model.   *)
