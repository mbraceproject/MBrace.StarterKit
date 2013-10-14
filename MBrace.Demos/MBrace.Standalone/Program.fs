// Learn more about F# at http://fsharp.net
// See the 'F# Tutorial' project for more help.

open Nessos.MBrace.Client

[<EntryPoint>]
let main argv =
    MBraceSettings.MBracedExecutablePath <- @"C:\Program Files (x86)\MBrace\bin\mbraced.exe"
    MBraceSettings.StoreProvider <- LocalFS

    let runtime = MBrace.InitLocal 3 

    let x = runtime.Run <@ cloud { return 1 + 1 } @>
    
    x // return an integer exit code
