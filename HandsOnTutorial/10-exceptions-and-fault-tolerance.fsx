(*** hide ***)
#load "ThespianCluster.fsx"
//#load "AzureCluster.fsx"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit AzureCluster.fsx to enter your connection strings.

open System
open System.IO
open MBrace.Core
open MBrace.Core.BuilderAsyncExtensions
open MBrace.Flow

// Initialize client object to an MBrace cluster
let cluster = Config.GetCluster() 

(**

# Exceptions and Fault tolerance

> This tutorial is from the [MBrace Starter Kit](https://github.com/mbraceproject/MBrace.StarterKit).

In this tutorial we will be offering an overview of the MBrace exception handling
features as well as its fault tolerance mechanism.

## Exception handling

Just like async, mbrace workflows support exception handling:

*)

cloud { do failwith "kaboom!" } |> cluster.Run

(**

Sending the above computation to your cluster will have the expected behaviour,
any user exception will be caught and rethrown on the client side:

    [lang=console]
    System.Exception: kaboom!
       at FSI_0010.it@24-3.Invoke(Unit unitVar)
       at MBrace.Core.BuilderImpl.Invoke@98.Invoke(Continuation`1 c) in C:\Users\eirik\Development\mbrace\MBrace.Core\src\MBrace.Core\Continuation\Builders.fs:line 98
    --- End of stack trace from previous location where exception was thrown ---
       at <StartupCode$MBrace-Runtime>.$CloudProcess.AwaitResult@211-2.Invoke(CloudProcessResult _arg2) in C:\Users\eirik\Development\mbrace\MBrace.Core\src\MBrace.Runtime\Runtime\CloudProcess.fs:line 211
       at Microsoft.FSharp.Control.AsyncBuilderImpl.args@835-1.Invoke(a a)
       at MBrace.Core.Internals.AsyncExtensions.Async.RunSync[T](FSharpAsync`1 workflow, FSharpOption`1 cancellationToken) in C:\Users\eirik\Development\mbrace\MBrace.Core\src\MBrace.Core\Utils\AsyncExtensions.fs:line 99
       at <StartupCode$FSI_0010>.$FSI_0010.main@() in C:\Users\eirik\Development\mbrace\MBrace.StarterKit\HandsOnTutorial\10-exceptions-and-fault-tolerance.fsx:line 24
    Stopped due to error

This has interesting ramifications when our cloud computation spans multiple machines:

*)

cloud {
    let div m n = cloud { return m / n }
    let! results = Cloud.Parallel [for i in 1 .. 10 -> div 10 (5 - i) ]
    return Array.sum results
}

(**

In the example above, we perform a calculation in parallel across the cluster
in which one of the child work items are going to fail with a user exception.
In the event of such an uncaught error, the exception will bubble up to the parent computation,
actively cancelling any of the outstanding sibling computations:

    [lang=console]
    System.DivideByZeroException: Attempted to divide by zero.
       at FSI_0013.div@47-1.Invoke(Unit unitVar)
       at MBrace.Core.BuilderImpl.Invoke@98.Invoke(Continuation`1 c) in C:\Users\eirik\Development\mbrace\MBrace.Core\src\MBrace.Core\Continuation\Builders.fs:line 98
       at Cloud.Parallel(seq<Cloud<Int32>> computations)
    --- End of stack trace from previous location where exception was thrown ---
       at <StartupCode$MBrace-Runtime>.$CloudProcess.AwaitResult@211-2.Invoke(CloudProcessResult _arg2) in C:\Users\eirik\Development\mbrace\MBrace.Core\src\MBrace.Runtime\Runtime\CloudProcess.fs:line 211
       at Microsoft.FSharp.Control.AsyncBuilderImpl.args@835-1.Invoke(a a)
       at MBrace.Core.Internals.AsyncExtensions.Async.RunSync[T](FSharpAsync`1 workflow, FSharpOption`1 cancellationToken) in C:\Users\eirik\Development\mbrace\MBrace.Core\src\MBrace.Core\Utils\AsyncExtensions.fs:line 99
       at <StartupCode$FSI_0014>.$FSI_0014.main@()

While the stacktrace offers a precise indication of what went wrong,
it may be a bit ambiguous on *how* and *where* it went wrong.
Let's see how we can use MBrace to improve this in our example:

*)

exception WorkerException of worker:IWorkerRef * input:int * exn:exn
with
    override e.Message = sprintf "Worker '%O' given input %d has failed with exception: '%O'" e.worker e.input e.exn


cloud {
    let div n = cloud { 
        try return 10 / (5 - n)
        with e -> 
            let! currentWorker = Cloud.CurrentWorker
            return raise (WorkerException(currentWorker, n, e))
    }

    let! results = Cloud.Parallel [for i in 1 .. 10 -> div i ]
    return Array.sum results
}

(**

Which yields the following stacktrace:

    [lang=console]
    FSI_0023+WorkerException: Worker 'mbrace://grothendieck:52789' given input 5 has failed with exception: 'System.DivideByZeroException: Attempted to divide by zero.
       at FSI_0024.div@88-32.Invoke(Unit unitVar)
       at MBrace.Core.BuilderImpl.Invoke@98.Invoke(Continuation`1 c) in C:\Users\eirik\Development\mbrace\MBrace.Core\src\MBrace.Core\Continuation\Builders.fs:line 98'
       at FSI_0024.div@91-34.Invoke(IWorkerRef _arg2)
       at MBrace.Core.Builders.Bind@331-1.Invoke(ExecutionContext ctx, T t) in C:\Users\eirik\Development\mbrace\MBrace.Core\src\MBrace.Core\Continuation\Builders.fs:line 331
       at Cloud.Parallel(seq<Cloud<Int32>> computations)
    --- End of stack trace from previous location where exception was thrown ---
       at <StartupCode$MBrace-Runtime>.$CloudProcess.AwaitResult@211-2.Invoke(CloudProcessResult _arg2) in C:\Users\eirik\Development\mbrace\MBrace.Core\src\MBrace.Runtime\Runtime\CloudProcess.fs:line 211
       at Microsoft.FSharp.Control.AsyncBuilderImpl.args@835-1.Invoke(a a)
       at MBrace.Core.Internals.AsyncExtensions.Async.RunSync[T](FSharpAsync`1 workflow, FSharpOption`1 cancellationToken) in C:\Users\eirik\Development\mbrace\MBrace.Core\src\MBrace.Core\Utils\AsyncExtensions.fs:line 99
       at <StartupCode$FSI_0025>.$FSI_0025.main@()

It is also possible to catch exceptions raised by distributed workflows:

*)

cloud {
    let div m n = cloud { return m / n }
    try
        let! results = Cloud.Parallel [for i in 1 .. 10 -> div 10 (5 - i) ]
        return Some(Array.sum results)
    with :? DivideByZeroException ->
        return None
}

(**

this will suppress any exceptions raised by the `Parallel` workflow and return a proper value to the client.

### Computing partial results

It is often the case that this default behaviour (i.e. bubbling up and cancellation) may be undesirable,
particularly when we are running an expensive distributed computation. Often, aggregating partial results
is the prefered way to go. Let's see how we can encode this behaviour using MBrace:

*)

cloud {
    let div m n = cloud { try return Choice1Of2(m / n) with e -> return Choice2Of2 e }
    let! results = Cloud.Parallel [for i in 1 .. 10 -> div 10 (5 - i) ]
    return results
}

(**

Which when executed will result in the following value:

    [lang=console]
    val it : Choice<int,exn> [] =
      [|Choice1Of2 2; Choice1Of2 3; Choice1Of2 5; Choice1Of2 10;
        Choice2Of2
          System.DivideByZeroException: Attempted to divide by zero.
       at FSI_0031.div@140-47.Invoke(Unit unitVar)
       at MBrace.Core.BuilderImpl.Invoke@98.Invoke(Continuation`1 c) in C:\Users\eirik\Development\mbrace\MBrace.Core\src\MBrace.Core\Continuation\Builders.fs:line 98
            {Data = dict [];
             HResult = -2147352558;
             HelpLink = null;
             InnerException = null;
             Message = "Attempted to divide by zero.";
             Source = "FSI-ASSEMBLY_f0c42c06-f5a8-45d0-ab7b-2fec2628dff0_10";
             StackTrace = "   at FSI_0031.div@140-47.Invoke(Unit unitVar)
       at MBrace.Core.BuilderImpl.Invoke@98.Invoke(Continuation`1 c) in C:\Users\eirik\Development\mbrace\MBrace.Core\src\MBrace.Core\Continuation\Builders.fs:line 98";
             TargetSite = null;}; Choice1Of2 -10; Choice1Of2 -5; Choice1Of2 -3;
        Choice1Of2 -2; Choice1Of2 -2|]

## Fault tolerance

It is important at this point to make a distinction between *user exceptions*,
i.e. runtime errors generated by user code and *faults*, errors that happen
because of problems in an MBrace runtime. Faults can happen for a multitude of reasons:

  * Bugs in the runtime implementation.
  * Sudden death of a worker node: VMs of a cloud service can often be reset by the
    administrator without warning.
  * User errors that can cause the worker process to crash like stack overflows.

Let's have a closer look at an example of a fault, so that we gain a better understanding
of how they work. First, we define a cloud function that forces the death of a worker
by calling `Environment.Exit` on the process that it is being executed:

*)

let die () = cloud { Environment.Exit 1 }

(**

Let's try to run this on our cluster:

*)

cluster.Run(die())

(**

Sure enough, after a while we will be receiving the following exception:

    [lang=console]
    MBrace.Core.FaultException: Work item '7927b7ad-f3ee-46cb-928b-92683f279722' was being processed by worker 'mbrace://grothendieck:52789' which has died.
       at <StartupCode$MBrace-Runtime>.$CloudProcess.AwaitResult@211-2.Invoke(CloudProcessResult _arg2) in C:\Users\eirik\Development\mbrace\MBrace.Core\src\MBrace.Runtime\Runtime\CloudProcess.fs:line 211
       at Microsoft.FSharp.Control.AsyncBuilderImpl.args@835-1.Invoke(a a)
       at MBrace.Core.Internals.AsyncExtensions.Async.RunSync[T](FSharpAsync`1 workflow, FSharpOption`1 cancellationToken) in C:\Users\eirik\Development\mbrace\MBrace.Core\src\MBrace.Core\Utils\AsyncExtensions.fs:line 99
       at <StartupCode$FSI_0035>.$FSI_0035.main@() in C:\Users\eirik\Development\mbrace\MBrace.StarterKit\HandsOnTutorial\10-exceptions-and-fault-tolerance.fsx:line 194

Note that this computation has killed one of our worker instances.
If working with MBrace on Azure, the service fabric will ensure that the dead instance will be reset.
If working with MBrace on Thespian, you will have to manually replenish your cluster instance by calling 
the `cluster.AttachNewLocalWorkers()` method.

If we now call

*)

cluster.ShowProcesses()

(**

we can indeed verify that the last computation has faulted:

    [lang=console]
    Processes                                                                                                                                                                   

    Name                            Process Id         Status  Execution Time         Work items        Result Type         Start Time             Completion Time       
    ----                            ----------         ------  --------------         ----------        -----------         ----------             ---------------       
          67272653-9d80-403e-848a-8c99760cb943      Completed  00:00:00.5097248    0 /   0 /  11 /  11  Choice<int,exn> []  23/12/2015 1:36:38 μμ  23/12/2015 1:36:38 μμ 
          d1b47fe3-4cc0-4ad9-b22f-5ba2ec9f749e  UserException  00:00:00.8093416    0 /   0 /   5 /   5  int []              23/12/2015 1:58:10 μμ  23/12/2015 1:58:11 μμ 
          4e537251-0191-4afe-9a9a-fb3f966ee2ef        Faulted  00:07:07.1084813    0 /   1 /   1 /   2  unit                23/12/2015 2:48:11 μμ    

MBrace will respond to faults in our cloud process by raising a `FaultException`.
What differentiates fault exceptions from normal user exceptions is that they often
cannot be caught by exception handling logic in the user level. For instance:

*)

cloud { try return! die() with :? FaultException -> () } |> cluster.Run

(**

Will not have any effect in the outcome of the computation.
This happens because the exception handling clause is actually part
of the work item which was to be executed by the worker that was killed.
Compare this against

*)

cloud { 
    try 
        let! _ = Cloud.Parallel [die();die()]
        return ()
    with :? FaultException -> return () 
} |> cluster.Run

(**

which works as expected since the exception handling logic happens
on the parent work item.

### Working with fault policies

In stark contrast to our pathological `die()` example, most real faults
actually happen because of transient errors in cluster deployments.
It is often the case that we want our computation not to stop because
of such minor faults. This is where *fault policies* come into play.

When sending a computation to the cloud, a fault policy can be specified.
This indicates whether, and for how long a specific faulting computation should
be retried:

*)

/// die with a probability of 1 / N
let diePb (N : int) = cloud { if System.Random().Next(0, N) = 0 then return! die() }

let test() = cloud {
    let! N = Cloud.GetWorkerCount() // get the current number of workers
    let! _ = Cloud.Parallel [ for i in 1 .. N -> diePb N ]
    return ()
}

(**

the computation as defined above introduces a significant probability of faulting
at some point of its execution. We can compensate by applying the following a
more flexible retry policy:

*)

cluster.Run(test(), faultPolicy = FaultPolicy.WithMaxRetries 5)

(**

This will cause any faulting part of the computation to yield 
a FaultException only after it has faulted more than 5 times.

Fault polices can also be scoped in our cloud code:

*)

cloud {
    let! results1 = Cloud.Parallel [for i in 1 .. 10 -> cloud { return i * i }]
    let! results2 = 
        Cloud.Parallel [for i in 1 .. 10 -> diePb 10 ]
        |> Cloud.WithFaultPolicy (FaultPolicy.InfiniteRetries())

    return (results1, results2)
}

(**

The example above uses the inherited fault policy for the first parallel computation,
whereas the second parallel computation uses a custom fault policy, namely `InfiniteRetries`.


### Faults & Partial results

The question that now arises is how would it be possible to recover partial results of a
distributed computation in the presence of faults? This can be achieved through the use
of runtime introspection primitives:

*)

let run() = cloud {
    let! isPreviouslyFaulted = Cloud.IsPreviouslyFaulted
    if not isPreviouslyFaulted then do! die()
}

cluster.Run(run(), faultPolicy = FaultPolicy.WithMaxRetries 1)

(**

The `Cloud.IsPreviouslyFaulted` primitive gives true if the current work item
is part of a computation that has previously faulted and currently being retried.
We can use this knowledge to alter the execution of computation.

Let's now have a look at a more useful example. Let's define a parallel combinator
that returns partial results even in the presence of faults.
First, let's define a result type:

*)

type Result<'T> =
    | Success of 'T
    | Exception of exn
    | Fault of exn

(**

we now define our `protectedParallel` combinator:

*)

let protectedParallel (computations : Cloud<'T> seq) = cloud {
    let protect (computation : Cloud<'T>) = cloud {
        let! faultData = Cloud.TryGetFaultData()
        match faultData with
        | None -> // computation not faulted, execute normally
            try let! t = computation in return Success t
            with e -> return Exception e

        | Some faultData -> // computation previously faulted, return an exception
            return Fault faultData.FaultException
    }

    return! 
        computations 
        |> Seq.map protect
        |> Cloud.Parallel 
        |> Cloud.WithFaultPolicy (FaultPolicy.WithMaxRetries 1)
}

(**

the example makes use of the `Cloud.TryGetFaultData()` primitive to determine
whether the current computation is a retry of a previously faulted operation.

Let's now test our workflow:

*)

let test2() = cloud {
    let d = Random().Next(0,10)
    if d = 1 then do! die()
    if d < 5 then return failwithf "error %d" d
    else return d
}

protectedParallel [for i in 1 .. 10 -> test2() ] |> cluster.Run

(**

## Summary

In this tutorial, you've learned how to reason about exceptions and faults in MBrace.
Continue with further samples to learn more about the MBrace programming model.  

> Note, you can use the above techniques from both scripts and compiled projects. To see the components referenced 
> by this script, see [ThespianCluster.fsx](ThespianCluster.html) or [AzureCluster.fsx](AzureCluster.html).
*)