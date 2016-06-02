(*** hide ***)
#load "ThespianCluster.fsx"
//#load "AzureCluster.fsx"
//#load "AwsCluster.fsx"

#load "lib/utils.fsx"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit AzureCluster.fsx to enter your connection strings.

open System
open System.IO
open MBrace.Core
open MBrace.Flow

// Initialize client object to an MBrace cluster
let cluster = Config.GetCluster() 

(**

# Using C# DLLs and Native Components 

> This tutorial is from the [MBrace Starter Kit](https://github.com/mbraceproject/MBrace.StarterKit).

It is very simple to use C# DLLs, native DLLs and any nuget packages in your cloud computations.
For C# DLLs you just download and reference the packages as normal
in your F# scripting or other client application. The DLLs for the packages are automatically uploaded to 
the cloud workers as needed.  In a sense, you don't need to do anything special.

In this tutorial, you first reference some C# DLLs from the Math.NET NuGet package.
You also use native binaries from the Intel MKL library.
  
## Using C# DLLs Locally

First, you reference and use the packages on the local machine: *) 

#load @"../packages/MathNet.Numerics.FSharp/MathNet.Numerics.fsx"

open MathNet.Numerics
open MathNet.Numerics.LinearAlgebra

let matrix1 = Matrix<double>.Build.Random(10,10)
let vector1 = Vector<double>.Build.Random(10)

let product = vector1 * matrix1 

let check = (matrix1 * matrix1.Inverse()).Determinant()

(** 

## Using C# DLLs on the cluster

Next, run the code on MBrace. Note that the DLLs from the packages are uploaded automatically. 

The following inverts 100 150x150 matrices using C# code on the cluster. 

*) 
let csharpMathJob = 
    [ for i in 1 .. 100 -> 
         local { 
            Control.UseManaged()
            Control.UseSingleThread()
            let m = Matrix<double>.Build.Random(250,250) 
            return (m * m.Inverse()).Determinant()
         } ]
    |> Cloud.ParallelBalanced
    |> cluster.CreateProcess

// Show the progress
csharpMathJob.ShowInfo()


// Await the result, we expect an array of numbers very close to 1.0
let csharpMathResults = csharpMathJob.Result


(** 
## Running Native Code on the Cluster

Next, you run the same work using the Interl MKL native DLLs. 

> To upload native DLLs, register their paths as native dependencies. These will be included with all uploaded dependencies of the session.
*)

let contentDir = __SOURCE_DIRECTORY__ + "/../packages/MathNet.Numerics.MKL.Win-x64/content/"
cluster.RegisterNativeDependency (contentDir + "libiomp5md.dll")
cluster.RegisterNativeDependency (contentDir + "MathNet.Numerics.MKL.dll")

(** The first MKL job can take a while first time you run it, because 'MathNet.Numerics.MKL.dll' needs to be uploaded: *) 
let firstNativeJob = 
    cloud { 
        Control.UseNativeMKL()
        let m = Matrix<double>.Build.Random(250,250) 
        return (m * m.Inverse()).Determinant()
    }
    |> cluster.CreateProcess

// Check progress
firstNativeJob.ShowInfo()

// Wait for the result
firstNativeJob.Result

(** Now run a much larger job: 1000 250x250 matrices, inverted using Intel MKL: *)
let nativeMathJob = 
    [ for i in 1 .. 1000 -> 
         local { 
            Control.UseNativeMKL()
            Control.UseSingleThread()
            let m = Matrix<double>.Build.Random(250,250) 
            return (m * m.Inverse()).Determinant() 
         } ]
    |> Cloud.ParallelBalanced
    |> cluster.CreateProcess

// Check progress
nativeMathJob.ShowInfo()

// Wait for the result
nativeMathJob.Result

(** Once complete, you can compare the execution times of the jobs to see if using native code has improved performance: *) 
let timeNative = nativeMathJob.ExecutionTime.Value.TotalSeconds / 1000.0 
let timeCSharp = csharpMathJob.ExecutionTime.Value.TotalSeconds / 100.0  

let perfRatio = timeCSharp/timeNative 

(** 
## Summary

In this tutorial, you've learned how to use C# DLLs, NuGet packages and 
native DLLs in your MBrace computations. You've also compared performance between native code and 
C# code running on your cluster for one particular example.  
Continue with further samples to learn more about the MBrace programming model.  

> Note, you can use the above techniques from both scripts and compiled projects. To see the components referenced 
> by this script, see [ThespianCluster.fsx](ThespianCluster.html) or [AzureCluster.fsx](AzureCluster.html).
 *)
