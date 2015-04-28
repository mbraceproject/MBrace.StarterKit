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
 This example illustrates doing I/O tasks in parallel using the workers in the cluster
 
 Before running, edit credentials.fsx to enter your connection strings.
**)

// First connect to the cluster
let cluster = Runtime.GetHandle(config)

// Cloud parallel url-downloader
open System.Net

let urls = 
    [| ("bing", "http://bing.com")
       ("yahoo", "http://yahoo.com")
       ("google", "http://google.com")
       ("msn", "http://msn.com") |]

/// Cloud workflow to download a file and wave it into cloud storage
let download (name: string, uri: string) = 
    cloud {
        let webClient = new WebClient()
        let! text = webClient.AsyncDownloadString(Uri(uri)) |> Cloud.OfAsync
        do! CloudFile.Delete(sprintf "pages/%s.html" name)
        let! file = CloudFile.WriteAllText(text,path=sprintf "pages/%s.html" name)
        return file
    }

let filesJob = 
    urls 
    |> Array.map download
    |> Cloud.Parallel
    |> cluster.CreateProcess

// Check on progress...
filesJob.ShowInfo()

// Get the result of the job
let files = filesJob.AwaitResult()

// Read the files we just downloaded
let contentsOfFiles = 
    files
    |> Array.map (fun file ->
        cloud { let! text = CloudFile.ReadAllText(file)
                return (file.Path, text.Length) })
    |> Cloud.Parallel
    |> cluster.Run
