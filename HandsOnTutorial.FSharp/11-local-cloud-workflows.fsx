(*** hide ***)
#load "ThespianCluster.fsx"
//#load "AzureCluster.fsx"
//#load "AwsCluster.fsx"

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

# Local Cloud Workflows

> This tutorial is from the [MBrace Starter Kit](https://github.com/mbraceproject/MBrace.StarterKit).

In this tutorial we will be offering an description of local cloud workflows
and how they can be useful in avoiding common errors when developing for MBrace.

## Motivation

Cloud workflows are computations that often span multiple machines.
This means variables in scope often need to be serialized and sent
to a remote machine for resumption of the computation.
This leads to a new class of potential errors, as illustrated below:

*)

cloud {
    let wc = new System.Net.WebClient()
    let download uri = cloud { return! wc.AsyncDownloadString (Uri uri) }
    let! results = Cloud.Parallel [download "http://mbrace.io" ; download "http://fsharp.org" ]
    return results
}

(**

which when run yields

    [lang=bash]
    System.Runtime.Serialization.SerializationException: Cloud.Parallel<string> workflow 
    uses non-serializable closures. 
        ---> Nessos.FsPickler.NonSerializableTypeException: Type 'FSI_0005+download@37-1' contains 
    non-serializable field of type 'System.Net.WebClient'.

The obvious fix here is to remove the global `WebClient` instance, which is not serializable
and replace it with localized instantiations:

*)

cloud {
    let download uri = cloud { 
        let wc = new System.Net.WebClient()
        return! wc.AsyncDownloadString (Uri uri) 
    }

    let! results = Cloud.Parallel [download "http://mbrace.io" ; download "http://fsharp.org" ]
    return results
}

(**

The example however does illustrate a more general problem;
suppose we have a black-box cloud computation:

    [lang=fsharp]
    val comp : Cloud<int>

Dependending on the implementation, 
this could either introduce distribution:

*)

let comp : Cloud<int> = cloud {
    let f x = cloud { return x }
    let! results = Cloud.Parallel [f 17 ; f 25]
    return Array.sum results
}

(**

or no distribution at all:

*)

let comp' : Cloud<int> = cloud {
    let f x = cloud { return x }
    let! a = f 17
    let! b = f 25
    return a + b
}

(**

The two computations carry identical types,
yet their execution patterns are very different.
This can often lead to unanticipated errors,
for instance

*)

let test (arg : Cloud<'T>) = cloud {
    let wc = new System.Net.WebClient()
    let! _ = arg
    return wc.GetHashCode()
}

test comp  |> cluster.Run
test comp' |> cluster.Run

(**

which would fail with a runtime serialization error
only if `comp` entails distribution.

## Local Cloud Workflows

For the reasons outlined above, MBrace comes with *local* cloud workflows.
These are a special type of cloud computation which are necessarily constrained
to execute within a single worker. They can defined using the special `local` builder:

*)

let localWorkflow : LocalCloud<int> = local { return 42 }

(**

This creates workflows of type `LocalCloud<'T>`, which is a subtype of `Cloud<'T>`.
Local workflows can be safely used in place of cloud workflows, however the opposite is not possible.
In other words, the workflow below type checks:

*)

cloud {
    let! x = local { return 17 }
    return x + 25
}

(**

However, attempting to distribute inside the body of a local workflow:

*)

local {
    let f x = cloud { return x }
    let! x = Cloud.Parallel [f 17 ; f 25]
    return Array.sum x
}

(**

yields a compile-time error:

    [lang=bash]
    error FS0001: This expression was expected to have type
        LocalCloud<'a>    
    but here has type
        Cloud<int []>

In other words, local workflows provided a compile-time guarantee
that their execution will *never* execute beyond the context of
a single machine. This allows the MBrace library author to enforce
a certain degree of sanity with respect to serialization:

*)

let testLocal (arg : LocalCloud<'T>) = local {
    use wc = new System.Net.WebClient()
    let! _ = arg // execution guaranteed to not switch to separate machine
    return wc.GetHashCode() // hence 'wc' will only live within the context of a single address space
}

(**

## Applications

The MBrace core APIs already make heavy use of local workflows;
most store primitive operations are of type `LocalCloud<'T>` since
they usually do not entail distribution:

*)

local {
    let! files = CloudFile.Enumerate "/container"
    return files.Length
}

(**

The same happens with many library implementations:

    [lang=fsharp]
    open MBrace.Library.Cloud

    val Balanced.mapReduceLocal : 
        mapper:('T -> LocalCloud<'R>) -> reducer:('R -> 'R -> LocalCloud<'R>) 
            -> init:'R -> inputs:seq<'T> -> Cloud<'R>

In this case, a distributed cloud computation is created given user-supplied computations
that must be constrained to a single machine. This API restriction enables the library author
to efficiently schedule computation across the cluster based on the assumption that user code
will never escape the scope of a single machine per input.

## Gotchas

It is important to clarify that even though local workflows do not introduce distribution
*inside* their implementation, this does not imply that they are devoid of any serialization issues.
Let's illustrate using a couple of examples. Running

*)

local { return new System.Net.WebClient() } |> cluster.Run

(**

fails with the error

    [lang=bash]
    System.Runtime.Serialization.SerializationException: Cloud process returns non-serializable type 'System.Net.WebClient'.
        at MBrace.Runtime.Combinators.runStartAsCloudProcess@297.Invoke(Unit unitVar)

This happens because the local computation returns result which cannot be serialized. Similarly

*)

cloud {
    let wc = new System.Net.WebClient()
    let! proc = Cloud.CreateProcess(local { return wc.GetHashCode() })
    return proc.Result
} |> cluster.Run

(**

fails with the error

    [lang=bash]
    System.Runtime.Serialization.SerializationException: Cloud process of type 'int' uses non-serializable closure. 
        ---> Nessos.FsPickler.NonSerializableTypeException: Type 'FSI_0020+it@230-7' contains non-serializable field of type 'System.Net.WebClient'.

Since its closure has been rendered nonserializable due to its containing an instance of type `WebClient`.

## Summary

In this tutorial, you've learned how to use local workflows to avoid common errors
when developing in MBrace. Continue with further samples to learn more about the MBrace programming model.  

> Note, you can use the above techniques from both scripts and compiled projects. To see the components referenced 
> by this script, see [ThespianCluster.fsx](ThespianCluster.html) or [AzureCluster.fsx](AzureCluster.html).

*)