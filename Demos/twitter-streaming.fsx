// BEGIN PREAMBLE -- do not evaluate, for intellisense only
#r "Nessos.MBrace.Utils"
#r "Nessos.MBrace.Actors"
#r "Nessos.MBrace.Base"
#r "Nessos.MBrace.Store"
#r "Nessos.MBrace.Client"

open Nessos.MBrace.Client
// END PREAMBLE

#r "../External-Libs/FSharp.Data.dll"

type T = FSharp.Data.JsonProvider<"http://search.twitter.com/search.json?q=%23fsharp&lang=en&rpp=1&page=1">

let get (tag : string) (since : System.DateTime) =
    let enc = System.Web.HttpUtility.UrlEncode : string -> string
    T.Load(sprintf "http://search.twitter.com/search.json?q=%s&since=%4d-%02d-%02d" (enc tag) since.Year since.Month since.Day)




let jo = get "#fsharp" (System.DateTime.Parse("6/1/2013"))

jo.Results.JsonValue.


let tweets (tag : string) (since : System.DateTime) =
    let enc = System.Web.HttpUtility.UrlEncode : string -> string
    let rec page n =        
        let data = T.Load(sprintf "http://search.twitter.com/search.json?q=%s&rpp=100&page=%d&since=%4d-%02d-%02d" (enc tag) n since.Year since.Month since.Day)

        seq{
            yield! data.Results.JsonValue
            if not (Seq.isEmpty data.Results) then yield! page (n + 1)
        }
    page 1

// usage
tweets "#fsharp" (System.DateTime.Parse("5/17/2013"))
|> Seq.iter ( fun t -> printfn "%-21O %-15s %s" t.CreatedAt t.FromUser t.Text )