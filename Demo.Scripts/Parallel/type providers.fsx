#load "../../packages/MBrace.Runtime.0.5.13-alpha/bootstrap.fsx" 

open Nessos.MBrace
open Nessos.MBrace.Client

#r "../../packages/FSharp.Data.2.0.15/lib/net40/FSharp.Data.dll"

open FSharp.Data

let runtime = MBrace.InitLocal(totalNodes = 3)

[<Cloud>]
let atmCount () = cloud {
    let wb = WorldBankData.GetDataContext()
    let countries = wb.Regions.``European Union``.Countries
    return! 
        countries 
        |> Seq.map (fun c -> cloud { return c.Name, c.Indicators.``ATMs per 100,000 adults``.Values |> Seq.tryPick Some }) 
        |> Cloud.Parallel
}

runtime.Run <@ atmCount () @>