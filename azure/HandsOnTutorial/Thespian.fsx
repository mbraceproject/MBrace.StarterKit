#I "../../packages/MBrace.Thespian/tools" 
#I "../../packages/Streams/lib/net45" 
#r "../../packages/Streams/lib/net45/Streams.Core.dll"
#I "../../packages/MBrace.Flow/lib/net45" 
#r "../../packages/MBrace.Flow/lib/net45/MBrace.Flow.dll"
#load "../../packages/MBrace.Thespian/MBrace.Thespian.fsx"

namespace global

[<AutoOpen>]
module MBraceThespian = 

    open MBrace.Core
    open MBrace.Runtime
    open MBrace.Thespian

    let initThespianCluster (workerCount : int) =
        let cluster = MBraceCluster.InitOnCurrentMachine(workerCount, logger = new ConsoleLogger(), logLevel = LogLevel.Info)
        cluster :> MBraceClient