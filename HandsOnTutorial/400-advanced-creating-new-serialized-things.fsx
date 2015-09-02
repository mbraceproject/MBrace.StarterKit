(*** hide ***)
#load "Thespian.fsx"
#load "Azure.fsx"
#r "System.Runtime.Serialization.dll"

open System
open System.IO
open System.Runtime.Serialization
open MBrace.Core
open MBrace.Azure
open MBrace.Flow

// Initialize client object to an MBrace cluster:
let cluster = 
//    getAzureClient() // comment out to use an MBrace.Azure cluster; don't forget to set the proper connection strings in Azure.fsx
    initThespianCluster(4) // use a local cluster based on MBrace.Thespian; configuration can be adjusted using Thespian.fsx

(** 

This tutorial shows you how to build a new serializable abstraction, e.g. 
for another cloud asset referred to by name.  

*)

[<AutoSerializable(true) ; Sealed; DataContract>]
/// An abstract item that you want to be transparently serializable to the cluster
type CloudThing (data:string) =
    
    [<DataMember(Name = "Data")>]
    // The core data of the item
    let data = data

    [<IgnoreDataMember>]
    // Some derived data for the item
    let mutable derivedData = data.Length

    [<OnDeserialized>]
    let _onDeserialized (_ : StreamingContext) =
        // Re-establish the derived data on de-serialization
        derivedData <- data.Length

    /// Access the core data
    member __.Data = data

    /// Access the derived data
    member __.DerivedData = derivedData


//---------------------------------------------------------------------------
// Now use the serializable thing on the cluster

// The values to serialize
let data1 = CloudThing("hello")
let data2 = CloudThing("goodbye")

let job = 
  cloud { do! Cloud.Sleep 1000
          return data1.Data, data1.DerivedData, data2 }
   |> cluster.CreateCloudTask
     

job.ShowInfo()
job.Status
job.AwaitResult()
