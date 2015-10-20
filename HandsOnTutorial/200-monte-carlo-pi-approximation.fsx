(*** hide ***)
#load "ThespianCluster.fsx"
//#load "AzureCluster.fsx"

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

# Monte Carlo Pi Approximation

*)

/// the classic monte carlo implementation
/// take random points and see of they are inside the circle or not
let monteCarloWorker (iterations : int) =
    let rand = new Random(obj().GetHashCode())

    let mutable acc = 0
    for i = 1 to int iterations do
        let x = rand.NextDouble()
        let y = rand.NextDouble()
        if x * x + y * y <= 1. then
            acc <- acc + 1

    acc

/// Monte carlo worker that spans large number of iterations
let bigMonteCarloWorker (iterations : bigint) =
    let mutable rem = iterations
    let mutable acc = 0I
    while rem > 0I do
        let curr = min (bigint Int32.MaxValue) rem
        acc <- acc + bigint(monteCarloWorker (int curr))
        rem <- rem - curr

    acc

/// convert a bigint rational to float
let ratioToFloat divident divisor =
    let rec aux acc i dividend divisor =
        let div, rem = BigInteger.DivRem(dividend,divisor)
        if i = 15 || div = 0I && rem = 0I then acc else
        let dec = float div * Math.Pow(10., - float i)
        aux (acc + dec) (i + 1) (rem * 10I) divisor

    aux 0. 0 divident divisor

/// Calculate π
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
        |> CloudFlow.map bigMonteCarloWorker
        |> CloudFlow.sum

    let ratio = ratioToFloat samples iterations // expecting π / 4
    return 4.0 * ratio
}


let calcProc = cluster.CreateProcess(calculatePi 10000000000I)

cluster.ShowWorkers()
calcProc.ShowInfo()
calcProc.Result