(*** hide ***)
#load "../ThespianCluster.fsx"
//#load "../AzureCluster.fsx"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit AzureCluster.fsx to enter your connection strings.

#load "../../packages/RProvider/RProvider.fsx"
open System
open System.IO
open System.IO.Compression
open System.Net
open System.Numerics
open System.Diagnostics

open RDotNet
open RProvider
open RProvider.graphics
open RProvider.stats

open MBrace.Core
open MBrace.Library
open MBrace.Flow

// Initialize client object to an MBrace cluster
let cluster = Config.GetCluster() 

(**

# Using R Provider with MBrace

In this tutorial, you will learn how you can use MBrace to distribute code that utilises the 
[R Type Provider](http://bluemountaincapital.github.io/FSharpRProvider/).

## Installing R across you cluster

First of all, we define a bit of MBrace code that performs installation of R components on an MBrace cluster.
This assumes that worker processes are run with elevated permisions. 
As of MBrace.Azure v 1.1.5, bundled cloud service packages have elevated permissions enabled.
If your cluster does not come with elevated permissions, please ensure that R is already installed across your workers.

*)

/// Path to R installer mirror; change as appropriate
let R_Installer = "http://cran.cnr.berkeley.edu/bin/windows/base/R-3.2.2-win.exe"

/// checks whether R is installed in the local computer
let isRInstalled() = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\R-core") <> null

/// Performs R installation operation on an MBrace cluster
/// Assumes workers running with elevated privileges
let installR () = cloud {
    let installRToCurrentWorker() = local {
        if not <| isRInstalled() then
            do! Cloud.Logf "Installing R in local machine."
            use wc = new System.Net.WebClient()
            let tmp = Path.GetTempPath()
            let tmpExe = Path.Combine(tmp, Path.ChangeExtension(Path.GetRandomFileName(),".exe"))
            do! Cloud.Logf "Downloading R bits..."
            do wc.DownloadFile(Uri R_Installer, tmpExe)
            do! Cloud.Logf "Installing R..."
            let psi = new ProcessStartInfo(tmpExe, "/COMPONENTS=x64,main,translation /SILENT")
            psi.UseShellExecute <- false
            let proc = Process.Start(psi)
            proc.WaitForExit()
            if proc.ExitCode <> 0 then invalidOp "failed to install R in local context"
            do! Cloud.Logf "R installation complete."
    }

    // performs install operation for every worker in the current cluster   
    let! _ = Cloud.ParallelEverywhere(installRToCurrentWorker())
    return ()
}

/// Parallel workflow that verifies whether R is successfully installed across the cluster
let isRInstalledCloud() = cloud {
    let! results = Cloud.ParallelEverywhere (cloud { return isRInstalled ()})
    return Array.forall id results
}

(**

We can now install R across the cluster by calling
*)

installR() |> cluster.Run

(**

And verify that the operation was successful

*)

isRInstalledCloud() |> cluster.Run

(**

## Using the R provider with MBrace

We are now ready to begin using the R type provider with MBrace.
Here is a simple, non-parallel example taken from the R Type Provider [tutorial](http://bluemountaincapital.github.io/FSharpRProvider/Statistics-QuickStart.html).

*)

let testR() = cloud {
    // Random number generator
    let rng = Random()
    let rand () = rng.NextDouble()

    // Generate fake X1 and X2 
    let X1s = [ for i in 0 .. 9 -> 10. * rand () ]
    let X2s = [ for i in 0 .. 9 -> 5. * rand () ]

    // Build Ys, following the "true" model
    let Ys = [ for i in 0 .. 9 -> 5. + 3. * X1s.[i] - 2. * X2s.[i] + rand () ]

    let dataset =
        namedParams [
            "Y", box Ys;
            "X1", box X1s;
            "X2", box X2s; ]
        |> R.data_frame

    let result = R.lm(formula = "Y~X1+X2", data = dataset)

    let coefficients = result.AsList().["coefficients"].AsNumeric()
    let residuals = result.AsList().["residuals"].AsNumeric()
    return coefficients.ToArray(), residuals.ToArray()
}

cluster.Run (testR())

(**

In this tutorial, you've learned how to use the R type provider using MBrace.

Continue with further samples to learn more about the MBrace programming model.

> Note, you can use the above techniques from both scripts and compiled projects. To see the components referenced 
> by this script, see [ThespianCluster.fsx](../ThespianCluster.html) or [AzureCluster.fsx](../AzureCluster.html).
*)