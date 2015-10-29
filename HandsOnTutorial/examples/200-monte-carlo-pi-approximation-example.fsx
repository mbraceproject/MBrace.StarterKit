(*** hide ***)
#load "../ThespianCluster.fsx"
//#load "../AzureCluster.fsx"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit AzureCluster.fsx to enter your connection strings.

open System
open System.Numerics
open System.IO


open MBrace.Core
open MBrace.Flow

// Initialize client object to an MBrace cluster
let cluster = Config.GetCluster() 

(**

# Example: Monte Carlo Pi Approximation

In this example you learn you to perform distributed, multi-core CPU computations
on your MBrace cluster.

The example implements the classic monte carlo implementation using MBrace.
Take random points in the [0,1] x [0,1] square and count the occurences within the circle radius.
The resulting fraction should estimate π / 4.

First, define a local, single-threaded Pi estimator:

*)

let localMonteCarloPiWorker (iterations : bigint) : bigint =
    let rand = new Random(obj().GetHashCode())
    let maxIter = bigint Int32.MaxValue
    let mutable rem = iterations
    let mutable acc = 0I
    while rem > 0I do
        // bigints are heap allocated so break iteration into smaller int segments
        let iter = min maxIter rem
        let mutable currAcc = 0
        for i = 1 to int iter do
            let x = rand.NextDouble()
            let y = rand.NextDouble()
            if x * x + y * y <= 1. then
                currAcc <- currAcc + 1

        acc <- acc + bigint currAcc
        rem <- rem - iter

    acc

(**
Next, define a helper function to convert a bigint rational to floating point number:
*)

let ratioToFloat divident divisor =
    let rec aux acc i dividend divisor =
        let div, rem = BigInteger.DivRem(dividend,divisor)
        if i = 15 || div = 0I && rem = 0I then acc else
        let dec = float div * Math.Pow(10., - float i) // div * 10^-i
        aux (acc + dec) (i + 1) (rem * 10I) divisor

    aux 0. 0 divident divisor

(**

We are now ready to create the distributed component of our sample.
Now calculate π using MBrace:

*)

let calculatePi (iterations : bigint) : Cloud<float> = cloud {
    let! workers = Cloud.GetAvailableWorkers()
    let totalCores = workers |> Array.sumBy (fun w -> w.ProcessorCount) |> bigint
    let iterationsPerCore = iterations / totalCores
    let rem = iterations % totalCores
    let partitions =
        [|
            for _ in 1I .. totalCores -> iterationsPerCore
            if rem > 1I then yield rem
        |]

    // perform the distributed sampling operation
    let! samples =
        CloudFlow.OfArray partitions
        |> CloudFlow.map localMonteCarloPiWorker
        |> CloudFlow.sum

    let ratio = ratioToFloat samples iterations
    return 4.0 * ratio
}

(** Start the cloud process: *)

let calcTask = cluster.CreateProcess(calculatePi 10000000000I)

(** Check on its progress: *)

cluster.ShowWorkers()
calcTask.ShowInfo()

(** Get the result: *)
calcTask.Result

(** 
In this example, you've learned how to run a CPU-intensive Monte Carlo algorithm 
on an MBrace cluster. Continue with further samples to learn more about the
MBrace programming model.   


> Note, you can use the above techniques from both scripts and compiled projects. To see the components referenced 
> by this script, see [MBrace.Thespian.fsx](MBrace.Thespian.html) or [MBrace.Azure.fsx](MBrace.Azure.html).
*)
