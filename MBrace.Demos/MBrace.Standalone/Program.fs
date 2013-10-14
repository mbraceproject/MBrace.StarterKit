// Learn more about F# at http://fsharp.net
// See the 'F# Tutorial' project for more help.

open Nessos.MBrace.Client

[<EntryPoint>]
let main argv =
    MBraceSettings.MBracedExecutablePath <- @"C:\Program Files (x86)\MBrace\bin\mbraced.exe"
    MBraceSettings.StoreProvider <- LocalFS

    let runtime = 
        if argv.Length = 0 || argv.[0] = "local" then
            MBrace.InitLocal 3 
        else
            MBrace.Connect argv.[0]

    let x = runtime.Run <@ cloud { return 1 + 1 } @>
    
    printfn "Returned %A "x

    exit 0
