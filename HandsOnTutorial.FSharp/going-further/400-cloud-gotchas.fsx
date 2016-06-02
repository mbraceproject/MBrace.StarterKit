(*** hide ***)
#load "../ThespianCluster.fsx"
//#load "../AzureCluster.fsx"
//#load "../AwsCluster.fsx"

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

# Cloud Gotchas

This chapter explores more advanced topics of MBrace and the cloud.
In particular, we will look at common misconceptions and errors that occur
when programming in MBrace.

Follow the instructions and complete the assignments described below.

## Local vs. Remote execution

MBrace makes it possible to execute cloud workflows in the local process
just as if they were asynchronous workflows: parallelism is achieved using the local threadpool.
This can be done using the `cluster.Runlocally()` method:

*)

cloud { return Environment.MachineName } |> cluster.Run         // remote execution
cloud { return Environment.MachineName } |> cluster.RunLocally  // local execution

(**

As demonstrated above, local versus remote execution comes with minute differences w.r.t.
to the computed result as well as observed side-effects.

Let's try a simple example. Just by looking at the example below, 
can you guess what the difference will be when run locally as opposed to remotely?

*)

cloud { let _ = printfn "I am a side-effect!" in return 42 }

(**

While the above is a mostly harmless example, what can be said about the example below?

*)

open System.IO
let currentDirectory = Directory.GetCurrentDirectory()
let getContents = cloud { return Directory.EnumerateFiles currentDirectory |> Seq.toArray }

cluster.RunLocally getContents
cluster.Run getContents

(**

Why does the error happen? Can you suggest a way the above could be fixed?


## Cloud workflows and serialization I

It is often the case that our code relies on objects that are not serializable.
But what happens when this code happens to be running in the cloud?

*)

let downloader = cloud {
    let client = new System.Net.WebClient()
    let! downloadProc = Cloud.CreateProcess(cloud { return client.DownloadString("www.fsharp.org") })
    return downloadProc.Result
}

(**

What will happen if we attempt to execute the snippet above?

*)

cluster.Run(downloader)

(**

Assingment: can you rewrite the snippet above so that it no longer fails?
Tip: can you detect what segments of the code entail transition to a different machine?

## Cloud workflows and serialization II

Let us now consider the following type implementation:

*)

type Session() =
    let cluster = cluster
    let value = 41

    member s.Increment() =
        cluster.Run(cloud { return value + 1 })

(**

Can you predict what will happen if we run the following line?

*)

Session().Increment()

(**

Can you fix the problem only by changing the Increment() implementation?

Now, let's try the following example:

*)

module Session2 =
    let cluster = cluster
    let value = 41
    let increment() = cluster.Run(cloud { return value + 1})

Session2.increment()

(**

Can you explain why the behaviour of the above differs from the original example?


## Cloud workflows and object identity

Consider the following snippet:

*)

let example2 = cloud {
    let data = [| 1 .. 100 |]
    let! proc = Cloud.CreateProcess(cloud { return data })
    return Object.ReferenceEquals(data, proc.Result) 
}

(**

Can you guess its result?

*)

cluster.Run example2
cluster.RunLocally example2

(**

Can you explain why this behaviour happens?


## Cloud workflows and mutation

Consider the following sample:

*)

let example3 = cloud {
    let data = [|1 .. 10|]
    let! _ = Cloud.Parallel [for i in 0 .. data.Length - 1 -> cloud { data.[i] <- 0 } ]
    return data
}

(**

Can you guess its result?

*)

cluster.Run example3
cluster.RunLocally example3

(**

Can you explain why this behaviour happens?

## Summary

In this tutorial, you've learned how to reason about exceptions and faults in MBrace.
Continue with further samples to learn more about the MBrace programming model.  

> Note, you can use the above techniques from both scripts and compiled projects. To see the components referenced 
> by this script, see [ThespianCluster.fsx](ThespianCluster.html) or [AzureCluster.fsx](AzureCluster.html).

*)