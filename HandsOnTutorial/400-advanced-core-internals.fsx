#r "../packages/MBrace.Core/lib/net45/MBrace.Core.dll"

open MBrace.Core

(**

 This tutorial demonstrates MBrace workflow internals and offers a simple, thread-pool runtime implementation.

*)

// 1. Create a simple workflow

let hello = cloud { printfn "hello world" }

// 2. Execute the workflow locally

open MBrace.Core.Internals // access MBrace internals

//#nowarn "444" // uncomment to disable compiler warnings

Cloud.RunSynchronously(hello, ResourceRegistry.Empty)
Cloud.Start(hello, ResourceRegistry.Empty)

// 3. Run cloud workflow with user-defined continuations

let cont<'T> =
    {
        Success = fun _ (t : 'T) -> printfn "Success %A" t
        Exception = fun _ (edi : ExceptionDispatchInfo) -> printfn "Exception %O" (edi.Reify())
        Cancellation = fun _ _ -> printfn "Canceled"
    }

Cloud.StartWithContinuations(cloud { return 42 }, cont, ResourceRegistry.Empty)
Cloud.StartWithContinuations(cloud { failwith "boom"}, cont, ResourceRegistry.Empty)

// 5. Cloud.FromContinuations

let ret t = Cloud.FromContinuations(fun ctx cont -> printfn "returning %A" t ; cont.Success ctx t)

Cloud.RunSynchronously(ret 42, ResourceRegistry.Empty)

// 5. Cloud.Parallel:

let parallelWorkflow = Cloud.Parallel [ cloud { return 42 } ; cloud { return 42 } ]

Cloud.RunSynchronously(parallelWorkflow, ResourceRegistry.Empty) // fails with ResourceNotFoundException

// 6. Example: implementing our own brand of parallelism

// define an abstract parallelism combinator provider
type IParallelProvider =
    abstract Parallel : workflows: Cloud<'T> [] -> Cloud<'T []>

// Create a thread pool implementation
type ThreadPoolParallel () =
    // execute workflow with provided continuations as thread pool work item
    let runInThreadPool ctx scont econt ccont (workflow : Cloud<'T>) =
        let cont = { Success = scont ; Exception = econt ; Cancellation = ccont }
        System.Threading.ThreadPool.QueueUserWorkItem(fun _ -> Cloud.StartWithContinuations(workflow, cont, ctx)) |> ignore
        
    interface IParallelProvider with
        member __.Parallel(workflows : Cloud<'T> []) =
            Cloud.FromContinuations(fun ctx cont ->
                match workflows with
                | [||] -> cont.Success ctx [||]
                | _ ->
                    let n = workflows.Length
                    let results = Array.zeroCreate<'T> n
                    let completed = ref 0

                    // child success continuation
                    let onSuccess i (ctx : ExecutionContext) (t : 'T) =
                        results.[i] <- t
                        if System.Threading.Interlocked.Increment completed = n then
                            // all children completed, call parent success continuation
                            cont.Success ctx results
                      
                    // child exception continuation      
                    let errored = ref 0
                    let onError (ctx : ExecutionContext) exn =
                        if System.Threading.Interlocked.Increment errored = 1 then
                            cont.Exception ctx exn

                    for i = 0 to workflows.Length - 1 do
                        runInThreadPool ctx (onSuccess i) onError cont.Cancellation workflows.[i])


// define the parallelism combinator that infers implementation from
// execution context
type Cloud with
    // use parallelism as provided by execution context
    static member MyParallel(workflows : seq<Cloud<'T>>) : Cloud<'T []> = cloud {
        let! provider = Cloud.GetResource<IParallelProvider> ()
        return! provider.Parallel(Seq.toArray workflows)
    }

    // run parallelism with thread pool implementation pushed to context
    static member Run(workflow : Cloud<'T>) =
        // populate a resource registry with our own thread pool parallel provider implementation
        let resources = ResourceRegistry.Empty.Register<IParallelProvider>(new ThreadPoolParallel())
        Cloud.RunSynchronously(workflow, resources)


// test the workflow
Cloud.Run (Cloud.MyParallel [for i in 1 .. 100 -> cloud { return i}])