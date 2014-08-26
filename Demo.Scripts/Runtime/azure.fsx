#load "../../packages/MBrace.Runtime.0.5.4-alpha/bootstrap.fsx" 

open Nessos.MBrace
open Nessos.MBrace.Client
open Nessos.MBrace.Store

//  Running MBrace on Windows Azure
//
//  Please refer to http://nessos.github.io/MBrace/azure-tutorial.html for more information

#I "../../packages/MBrace.Azure.0.5.4-alpha/lib/net45/"
#r "MBrace.Azure.dll"

open Nessos.MBrace.Azure

let azureStore = AzureStore.Create(accountName = "accountName", accountKey = "accountKey")
MBraceSettings.DefaultStore <- azureStore

let nodes =
    [
        MBraceNode.Connect("10.0.0.4", 2675)
        MBraceNode.Connect("10.0.0.5", 2675)
        MBraceNode.Connect("10.0.0.6", 2675)
    ]

let runtime = MBrace.Boot(nodes, store = azureStore)

runtime.Run <@ cloud { return 42 } @>