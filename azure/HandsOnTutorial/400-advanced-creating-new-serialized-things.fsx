#load "credentials.fsx"
#r "System.Runtime.Serialization"

open System
open System.IO
open System.Runtime.Serialization

open MBrace.Core
open MBrace.Azure

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

(** First you connect to the cluster: *)
let cluster = MBrace.Azure.Client.Runtime.GetHandle(config)


// The values to serialize
let data1 = CloudThing("hello")
let data2 = CloudThing("goodbye")

let job = 
  cloud { do! Cloud.Sleep 1000
          return data1.Data, data1.DerivedData, data2 }
   |> cluster.CreateProcess
     

job.ShowInfo()
job.Completed
job.AwaitResult()
