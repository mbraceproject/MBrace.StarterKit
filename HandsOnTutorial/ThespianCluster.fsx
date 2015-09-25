#I "../packages/MBrace.Thespian/tools" 
#I "../packages/Streams/lib/net45" 
#r "../packages/Streams/lib/net45/Streams.Core.dll"
#I "../packages/MBrace.Flow/lib/net45" 
#r "../packages/MBrace.Flow/lib/net45/MBrace.Flow.dll"
#load "../packages/MBrace.Thespian/MBrace.Thespian.fsx"

namespace global

module Config = 

    open MBrace.Core
    open MBrace.Runtime
    open MBrace.Thespian

    // change to alter cluster size
    let private workerCount = 4
    
    let mutable private thespian = None
    /// Gets or creates a new Thespian cluster session.
    let GetCluster() = 
        match thespian with 
        | None -> thespian <- Some (ThespianCluster.InitOnCurrentMachine(workerCount, logger = new ConsoleLogger(), logLevel = LogLevel.Info))
        | Some t -> ()
        thespian.Value

    /// Kills the current cluster session
    let KillCluster() =
        match thespian with
        | None -> ()
        | Some t -> t.KillAllWorkers() ; thespian <- None