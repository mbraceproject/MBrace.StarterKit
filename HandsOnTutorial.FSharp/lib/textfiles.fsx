/// Collection of utilities for downloading text files from textfiles.com
[<RequireQualifiedAccess>]
module TextFiles

open System
open System.Collections.Concurrent
open System.IO
open System.Net
open System.Text
open System.Text.RegularExpressions

/// Crawls for text files found in textfiles.com
let crawlForTextFilesAsync () = async {
    let (@@) (uri : Uri) (path : string) = 
        let ub = UriBuilder(uri)
        ub.Path <- Path.Combine(ub.Path, path)
        ub.Uri

    let gathered = new ConcurrentDictionary<Uri, unit> ()
    let visited = new ConcurrentDictionary<Uri, unit> ()
    let href = new Regex("(?i)href(?-i)=\"([^\"]+)\"", RegexOptions.Compiled)
    let getRelativeLinks (html : string) = 
        let matches = href.Matches html
        matches 
        |> Seq.cast<Match> 
        |> Seq.map (fun m -> m.Groups.[1].Value)
        |> Seq.filter (fun m -> not (String.IsNullOrWhiteSpace m || Uri(m, UriKind.RelativeOrAbsolute).IsAbsoluteUri))
        |> Seq.toList

    let rec aux (dir : Uri) = async {
        if not <| visited.TryAdd(dir, ()) then return () else
        Console.WriteLine("Crawling '{0}'...", dir)
        let wc = new WebClient()
        let! html = wc.AsyncDownloadString(dir)
        // text files names directories using uppercase identifiers
        let dirs, files = getRelativeLinks html |> List.partition (fun u -> u.ToUpper() = u)

        do for f in files do gathered.[dir @@ f] <- ()

        return!
            dirs
            |> Seq.map (fun d -> aux (dir @@ d))
            |> Async.Parallel
            |> Async.Ignore
    }

    do! aux (Uri "http://www.textfiles.com/etext/")
    return gathered |> Seq.map (fun kv -> kv.Key.ToString()) |> Seq.toArray
}

/// Download provided text files to local directory
let downloadTextFilesAsync (localDir : string) (files : seq<string>) = async {
    let download (path : string) = async {
        let wc = new WebClient()
        let localPath = Path.Combine(localDir, Path.GetFileName path)
        let rec getFile i =
            let candidate = localPath + if i = 0 then "" else sprintf "-%d" i
            if File.Exists candidate then getFile (i+1)
            else candidate

        let localPath = getFile 0
        do! wc.AsyncDownloadFile(Uri path, localPath)
    }

    let _ = Directory.CreateDirectory localDir
    do! files |> Seq.map download |> Async.Parallel |> Async.Ignore
}

/// Crawls for text files found in textfiles.com
let crawlForTextFiles () = crawlForTextFilesAsync() |> Async.RunSynchronously

/// Download provided text files to local directory
let downloadTextFiles localDir files = downloadTextFilesAsync localDir files |> Async.RunSynchronously