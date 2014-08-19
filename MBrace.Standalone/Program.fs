// Learn more about F# at http://fsharp.net
// See the 'F# Tutorial' project for more help.

open Nessos.MBrace
open Nessos.MBrace.Store
open Nessos.MBrace.Client

let mbracedExe = __SOURCE_DIRECTORY__ + "/../packages/MBrace.Runtime.0.5.0-alpha/tools/mbraced.exe"

[<EntryPoint>]
let main argv =
    MBraceSettings.MBracedExecutablePath <- mbracedExe

    let isLocal, runtime =
        if argv.Length = 0 || argv.[0] = "local" then
            true, MBrace.InitLocal(totalNodes = 3, store = FileSystemStore.LocalTemp)
        else
            false, MBrace.Connect argv.[0]

    let x = runtime.Run <@ cloud { return 15 + 27 } @>    
    
    printfn "Returned %A " x

    if isLocal then runtime.Kill()

    exit 0