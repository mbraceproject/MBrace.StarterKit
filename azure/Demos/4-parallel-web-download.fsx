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
 This demo illustrates doing I/O tasks in parallel using the workers in the cluster
 
 Before running, edit credentials.fsx to enter your connection strings.
**)

// First connect to the cluster
let cluster = Runtime.GetHandle(config)

// Cloud parallel url-downloader
open System.Net

let urls = [| ("bing", "http://bing.com"); ("yahoo", "http://yahoo.com"); 
              ("google", "http://google.com"); ("msn", "http://msn.com") |]

let write (text: string) (stream: Stream) = async { 
    use writer = new StreamWriter(stream)
    writer.Write(text)
    return () 
}


/// Cloud workflow to download a file and wave it into cloud storage
let download (name: string, uri: string) = cloud {
    let webClient = new WebClient()
    let! text = Cloud.OfAsync (webClient.AsyncDownloadString(Uri(uri)))
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


/// Cloud workflow to read a cloud file as text and extract its length
let read (file: MBrace.CloudFile) = cloud {
    let! text = CloudFile.ReadAllText(file)
    return (file.Path, text.Length)
}

// Read the files we just downloaded
let contentsOfFiles = 
    files
    |> Array.map read
    |> Cloud.Parallel
    |> cluster.Run
