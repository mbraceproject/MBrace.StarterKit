// Assembly references for intellisense purposes only
#load "../../packages/MBrace.Runtime.0.5.7-alpha/bootstrap.fsx"

open Nessos.MBrace
open Nessos.MBrace.Client

(* This demo is a demonstration of the MutableCloudRef primitive
 * and a graph search algorithm using {mbrace}.
 * This demo takes as input two Wikipedia urls and finds a path of 
 * links driving you from the root url to the target url.
 * You should use this only for short searches because the demo
 * is crawling Wikipedia. For more complex searches you should
 * use Wikipedia dumps.
 *
 * The algorithm starts with a root GraphNode, gets all of it's children
 * GraphNodes and starts #children tasks using the choice operator to search
 * for the target GraphNode.
 * Each task does a Breadth First Search using it's own queue but all
 * of the tasks share a visited set implemented with MutableCloudRefs.
 *)

open System
open System.Net
open System.Collections.Generic
open System.Text.RegularExpressions


// Define a simple type for lookups based on MutableCloudRefs.
// A CloudSet needs a name (used as a container name by the mutables)
// and an encoding function. The encoding function is needed because
// we need to map each uri to a valid store filename
//  e.g. http://en.wikipedia.org is not a valid filesystem filename.
//  so an encode function could do base32 conversion.
type CloudSet = CloudSet of string * (string -> string)

// We do not need to store any data (so we use a IMutableCloudRef<unit>
// and we only need two operations:
//  - add an item
//  - check if exists
[<Cloud>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CloudSet =
    let tryAdd (item : string) (CloudSet (set_id, encode)) = cloud {
        try
            let! mref = MutableCloudRef.New(set_id, encode item, ())
            return true

        with :? StoreException -> return false
    }

    let contains (item : string) (CloudSet (set_id, encode)) = cloud {
        let! mref = MutableCloudRef.TryGet<unit>(set_id, encode item)
        return Option.isSome mref
    }

// The representation of a graph's GraphNode
// Each GraphNode has it's uri, a parent (in order to get the full path when we've found
// the target GraphNode), and a search depth.
type GraphNode = { Uri : string; Parent : GraphNode option; Depth : int } with

    // Download page, parse html and get children GraphNodes.
    member this.GetAdjacentNodes () : GraphNode seq =
        let is_black_listed uri = 
            uri = "Main_Page" || 
            Regex.IsMatch(uri, "\w+:.*") ||
            uri.StartsWith "List_" ||
            String.IsNullOrEmpty uri

        let regex = Regex("href=\"/wiki/(?<link>[^\"]+)\"", RegexOptions.Compiled)
        use wc = new WebClient(Proxy = null)
        let html = try wc.DownloadString this.Uri with :? WebException -> String.Empty

        regex.Matches html
        |> Seq.cast<Match>
        |> Seq.map (fun m -> m.Groups.["link"].Value )
        |> Seq.filter (not << is_black_listed)
        |> Seq.map (sprintf "http://en.wikipedia.org/wiki/%s")
        |> Seq.map (fun uri -> { Uri = uri; Parent = Some this; Depth = this.Depth + 1 })


// A classic BFS implementation, nothing fancy here 
[<Cloud>]
let bfs (root : GraphNode) (target : GraphNode) (visited : CloudSet) = cloud {
    let queue = Queue([root]) 

    let rec search_aux () = cloud {
        if Seq.isEmpty queue then return None
        else
            let t = queue.Dequeue()
            if t.Uri = target.Uri then 
                return Some t
            else
                let rec loop ns = cloud {
                    match ns with
                    | [] -> return None
                    | n :: _ when n.Uri = target.Uri -> return Some n
                    | n :: ns ->
                        let! exists = CloudSet.contains n.Uri visited
                        if not exists then
                            let! _ = CloudSet.tryAdd n.Uri visited
                            queue.Enqueue(n)
                        return! loop ns
                }
                let! r = loop <| (List.ofSeq <| t.GetAdjacentNodes ())
                match r with
                | Some _ as r -> return r
                | None -> return! search_aux ()
    }
    return! search_aux ()
}

// Nothing fancy here either just:
//  - take the root GraphNode
//  - expand the GraphNode
//  - check if the adjacent GraphNodes contain the target
//  - if not just start in parallel Breadth-First searches using the
//    choice operator
[<Cloud>]
let search (root : GraphNode) (target : GraphNode) visited = cloud {
    let adj = root.GetAdjacentNodes ()
    let nodes = adj |> Array.ofSeq

    match Seq.tryFind (fun n -> n.Uri = target.Uri) adj with
    | Some _ as result -> return result
    | None ->
        return! nodes |> Array.map (fun root -> bfs root target visited)
                      |> Cloud.Choice
}

// Get the path from the target GraphNode to its ancestors.
let rec get_path (t : GraphNode) = seq { 
    match t.Parent with
    | None   -> ()
    | Some p -> yield! get_path p 
    yield t.Uri
}

// Calling the search function, creating the CloudSet
[<Cloud>]
let main root target = cloud {
    let root   = { Uri = root;   Parent = None; Depth = 0  }
    let target = { Uri = target; Parent = None; Depth = -1 }
    
    let set_id = sprintf "set%d" <| Random().Next()   
    
    // You need to change this depending on the store provider
    // you're using.
    let encode (s : string) = System.Web.HttpUtility.UrlEncode(s)

    let visited = CloudSet(set_id, encode)

    let! result = search root target visited
    match result with
    | None -> return []
    | Some r -> return List.ofSeq (get_path r)
}


let runtime = MBrace.InitLocal(totalNodes = 4)

runtime.Run <@ main "http://en.wikipedia.org/wiki/Fsharp"
                    "http://en.wikipedia.org/wiki/Science" @>