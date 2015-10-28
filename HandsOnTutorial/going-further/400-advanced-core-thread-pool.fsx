(*** hide ***)
#I "../../packages/MBrace.Thespian/tools/"
#r "MBrace.Core.dll"
#r "MBrace.Runtime.dll"
#r "../../packages/MBrace.Flow/lib/net45/MBrace.Flow.dll"

open MBrace.Core
open MBrace.Core.BuilderAsyncExtensions
open MBrace.ThreadPool
open MBrace.Flow

let someDistributedCluster = Unchecked.defaultof<MBrace.Runtime.MBraceClient>

(**

# Running MBrace workflows in the Thread Pool

It is possible to run and test your MBrace workflows using the local thread pool.
To do this, you will need to reference [MBrace.Runtime](https://www.nuget.org/packages/MBrace.Runtime) library.

*)

open MBrace.ThreadPool

let tp = ThreadPoolRuntime.Create() // creates a thread pool handle

tp.RunSynchronously(cloud { printfn "Hello, World!"})

(**

The thread pool runtime can be used to test arbitrary cloud code in the confines of your current process:

*)

let test () = cloud {
    let! atom = CloudAtom.New<int>(0)
    let incr () = cloud { do! atom.UpdateAsync (fun i -> i + 1) }
    let! _ = Cloud.Parallel [for i in 1 .. 100 -> incr () ]
    return atom.Value
}

tp.RunSynchronously(test ())

(**

## Emulating distribution

By default, the thread pool runtime uses the expected async semantics, i.e. everything runs in shared memory.
However, when debugging cloud applications it is often useful to emulate conditions particular to distribution.
In the thread pool runtime this can be done by overriding the `MemoryEmulation` parameter, which is an enumeration
with three possible values:

  1. Shared: the default async semantics, values passed to child workflows are shared among worker threads.
  2. EnsureSerializable: values passed to child workflows are shared among worker threads, however checks are made
     at runtime to ensure that closures are serializable.

  3. Copied: values are passed to child workflows as cloned copies, ensuring proper emulation of distribution semantics.

Let's highlight the differences using a couple of examples:

*)

let example1 = cloud { return new System.Net.WebClient() }

(**

Running `example1` with the default shared semantics produces no error

*)

tp.RunSynchronously(example1, memoryEmulation = MemoryEmulation.Shared)

(**

however attempting to do the same with either of the other two options

*)

tp.RunSynchronously(example1, memoryEmulation = MemoryEmulation.EnsureSerializable)
tp.RunSynchronously(example1, memoryEmulation = MemoryEmulation.Copied)

(**

produces the error: 

```System.Runtime.Serialization.SerializationException: Cloud process returns non-serializable type 'System.Net.WebClient'.```

which is precisely what would happen if we were to run the computation in a distributed MBrace cluster. 
Consider now the following example

*)

let example2 = cloud {
    let i = ref 0
    let incr () = cloud { ignore <| System.Threading.Interlocked.Increment i }
    let! _ = Cloud.Parallel [for i in 1 .. 100 -> incr() ]
    return !i
}

(**

Running the example using `Shared` and `EnsureSerializable` emulation modes produces the same expected result (100):

*)

tp.RunSynchronously(example2, memoryEmulation = MemoryEmulation.EnsureSerializable)
tp.RunSynchronously(example2, memoryEmulation = MemoryEmulation.Shared)

(**

However, using `Copied` produces a wholly different output (0):

*)

tp.RunSynchronously(example2, memoryEmulation = MemoryEmulation.Copied)

(**

This is entirely consistent with the behaviour of the workflow in the cloud, 
since each child work item will be performing the increment operation in a copy of the value.

## Testing MBrace code locally before deploying

When working with a distributed MBrace cluster, it is easy to test your code locally before deploying
using the companion ThreadPool runtime available to every MBrace client instance:

*)

someDistributedCluster.RunLocally(example2, memoryEmulation = MemoryEmulation.Copied)

(**

When we are certain that everything works fine in the local process, we can confidently deploy as usual

*)

someDistributedCluster.CreateProcess(example2)

(**

## Gotchas

It should always be kept in mind that thread pool emulation does not sufficiently reproduce all
fine semantics variations that occur when executing the same workflow in the cloud. 
The simplest example are side-effects:

*)

let hello = cloud { printfn "Hello, World" }

tp.RunSynchronously(hello) // writes to stdout of the current process, effect easily observable

someDistributedCluster.Run(hello) // writes to stdout of some worker process, effect will most likely not be observed

(**

In general, side effects affecting global state behave differently in the local as opposed to the distributed setting:

*)

type GlobalCounter private () =
    static let mutable count = 0
    static member Value = count
    static member Incr() = System.Threading.Interlocked.Increment(&count)
    static member Reset() = count <- 0


let example3 = cloud {
    GlobalCounter.Reset()
    let! _ = Cloud.Parallel [for i in 1 .. 100 -> cloud { ignore <| GlobalCounter.Incr() }]
    return GlobalCounter.Value
}

tp.RunSynchronously(example3, memoryEmulation = MemoryEmulation.Copied) // 100, as expected

someDistributedCluster.Run(example3) // nondeterministic result

(**

## Summary

In this tutorial, you've learned about `MBrace.ThreadPool` as ways of executing and testing 
cloud workflows using the thread pool of your local client process.  

> Note, you can use the above techniques from both scripts and compiled projects. To see the components referenced 
> by this script, see [MBrace.Thespian.fsx](MBrace.Thespian.html) or [MBrace.Azure.fsx](MBrace.Azure.html).
*)