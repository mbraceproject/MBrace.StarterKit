[<AutoOpen>]
module Utils

#I __SOURCE_DIRECTORY__
#I "../../packages/MBrace.Azure/tools" 
#I "../../packages/Streams/lib/net45" 
#r "../../packages/Streams/lib/net45/Streams.dll"
#I "../../packages/MBrace.Flow/lib/net45" 
#r "../../packages/MBrace.Core/lib/net45/MBrace.Core.dll"
#r "../../packages/MBrace.Flow/lib/net45/MBrace.Flow.dll"

open MBrace.Core

/// Creates a new HashSet with provided sequence
let hashSet (ts : seq<'T>) = new System.Collections.Generic.HashSet<'T>(ts)

module Array = 
    let chunkBySize (n:int) (numbers: 'T[])  =
        if n <= 0 then invalidArg "n" "must be positive."

        [| for i in 1 .. numbers.Length / n  do 
            yield [| for j in ((i-1) * n) .. (i * n - 1) do 
                       yield numbers.[j] |] 
           if numbers.Length % n <> 0 then 
            yield [| for j in (numbers.Length / n) * n .. numbers.Length - 1 do 
                       yield numbers.[j] |] |] 

    let splitInto (n:int) (numbers: 'T[])  = 
        if n <= 0 then invalidArg "n" "must be positive."
        if numbers.Length < n then 
            numbers |> Array.map (fun t -> [| t |]) 
        else
            chunkBySize (numbers.Length / n) numbers

module List = 
    let chunkBySize (n:int) (numbers: 'T list)  =  numbers |> List.toArray |> Array.chunkBySize n |> Array.toList |> List.map Array.toList
    let splitInto (n:int) (numbers: 'T list)  =  numbers |> List.toArray |> Array.splitInto n |> Array.toList |> List.map Array.toList

module CloudFlow = 

    open MBrace.Flow

    let inline averageByKey (keyf: 'T -> 'Key) (valf: 'T -> 'Val) x =
        x |> CloudFlow.foldBy keyf  (fun (count, sum)  row -> (count + 1, sum + valf row)) 
            (fun (count,sum) (count2,sum2) -> (count + count2, sum + sum2))
            (fun () -> (0,LanguagePrimitives.GenericZero<'Val>))
        |> CloudFlow.map (fun (key, (count,sum)) -> (key, LanguagePrimitives.DivideByInt sum count))

    let inline sumByKey (keyf: 'T -> 'Key) (valf: 'T -> 'Val) x =
        x |> CloudFlow.foldBy keyf  (fun (sum: 'Val)  row -> (sum + valf row)) (+) (fun () -> LanguagePrimitives.GenericZero<'Val>)

    let inline percentageByKey (keyf: 'T -> 'Key) (filterf: 'T -> bool) x =
        x |> CloudFlow.foldBy keyf  (fun (count, sum)  row -> (count + 1L, if filterf row then sum + 1L else sum)) 
            (fun (count,sum) (count2,sum2) -> (count + count2, sum + sum2))
            (fun () -> (0L,0L))
        |> CloudFlow.map (fun (key, (count,sum)) -> (key, (float sum / float count)))


type Cloud with 
    /// Execute the single-machine cloud work items in parallel. Computation
    /// is balanced across the cluster according to multicore capacity.
    static member ParallelBalanced jobs = 
         jobs |> MBrace.Library.Cloud.Balanced.mapLocal id 

    /// Execute the single-machine cloud work items in parallel.
    /// Returns the immediate result once a positive result is found. Computation
    /// is balanced across the cluster according to multicore capacity.
    static member ChoiceBalanced jobs = 
         jobs |> MBrace.Library.Cloud.Balanced.tryPickLocal id 

    /// Execute the cloud work items on specific workers.
    static member ParallelOnSpecificWorkers (jobsAndWorkers: (#Cloud<'T> * IWorkerRef)[] ) = 
         MBrace.Core.Cloud.Parallel (jobsAndWorkers)
