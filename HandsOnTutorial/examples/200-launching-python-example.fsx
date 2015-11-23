(*** hide ***)
#load "../ThespianCluster.fsx"
//#load "../AzureCluster.fsx"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit AzureCluster.fsx to enter your connection strings.

#r "System.IO.Compression"
#r "System.IO.Compression.FileSystem"

open System
open System.IO
open System.IO.Compression
open System.Net
open System.Diagnostics
open MBrace.Core
open MBrace.Library
open MBrace.Flow

(**
# Example: Running python code using MBrace

> This tutorial is from the [MBrace Starter Kit](https://github.com/mbraceproject/MBrace.StarterKit).

In this tutorial, you can deploy and execute python code across an MBrace cluster.
This is achieved by defining a workflow that performs on-demand, per-worker installation of python bits.

*)

// Uri to python installation archive; modify as appropriate
// Alternatively this can be changed to a blob uri and could accomodate any type of software
let pythonBits = "https://www.python.org/ftp/python/3.5.0/python-3.5.0-embed-amd64.zip"

/// workflow that downloads and installs python to the local computer
let installPython () = local {
    let tmp = Path.GetTempPath()
    let! worker = Cloud.CurrentWorker
    let localDir = Path.Combine(tmp, sprintf "%s-p%d" (Path.GetFileNameWithoutExtension pythonBits) worker.ProcessId)
    if not <| Directory.Exists localDir then
        let localArchive = Path.Combine(tmp, sprintf "%s-p%d" (Path.GetFileName pythonBits) worker.ProcessId)
        do! Cloud.Logf "Downloading python..."
        use wc = new System.Net.WebClient()
        wc.DownloadFile(Uri pythonBits, localArchive)
        do! Cloud.Logf "Extracting installation..."
        use fs = File.OpenRead localArchive
        use za = new ZipArchive(fs)
        za.ExtractToDirectory(localDir)
        do! Cloud.Logf "Installation complete."

    let pythonExe = Path.Combine(localDir, "python.exe")
    if not <| File.Exists pythonExe then
        return failwith "Could not locate python.exe in the local installation."

    return pythonExe
}

(**

We now wrap the installation workflow in a [`DomainLocal`](http://mbrace.io/reference/core/mbrace-library-domainlocal.html) type.
This creates a serializable entity that will initialize the workflow exactly once in every AppDomain it is being executed.
Compare this to the [`ThreadLocal`](https://msdn.microsoft.com/en-us/library/dd642243(v=vs.110).aspx) class available in mscorlib.

*)

/// AppDomain-bound lazy python installer
let pythonInstaller = DomainLocal.Create(installPython())

/// Record containing results of a single python computation
type PythonResult =
    {
        StartTime : DateTimeOffset
        ExecutionTime : TimeSpan
        Stdout : string []
        Stderr : string []
        ExitCode : int
    }

/// Runs provided code in python and optional stdit inputs
/// returning the standard output as string
let runPythonScript (pythonCode : string) (stdin : string []) = local {
    // lazily install the python installation in the current machine
    // and retrieve the local executable
    let! pythonExe = pythonInstaller.Value
    // write python code to tmp file
    let pythonFile = Path.GetTempFileName()
    File.WriteAllText(pythonFile, pythonCode)
    // Launch the Python interpreter with provided arguments
    let prcInfo = ProcessStartInfo(pythonExe, 
                                    pythonFile, 
                                    UseShellExecute=false, 
                                    RedirectStandardInput=true, 
                                    RedirectStandardOutput=true,
                                    RedirectStandardError=true)

    let prc = new Process(StartInfo=prcInfo)
    let timer = new Stopwatch()
    let startTime = DateTimeOffset.Now
    timer.Start()
    prc.Start() |> ignore
    if stdin.Length > 0 then prc.StandardInput.Write(String.concat Environment.NewLine stdin)
    prc.StandardInput.Close()
    prc.WaitForExit()
    timer.Stop()
    let split (output:string) = output.Split([|Environment.NewLine|], StringSplitOptions.None)
    return {
        StartTime = startTime
        ExecutionTime = timer.Elapsed
        Stdout = prc.StandardOutput.ReadToEnd() |> split
        Stderr = prc.StandardError.ReadToEnd() |> split
        ExitCode = prc.ExitCode
    }
}

(**

We can now test this set up by running python code in the cloud.
Let's begin with a simple hello world example:

*)

let cluster = Config.GetCluster()

runPythonScript """print("Hello, World!") """ [||] |> cluster.Run

(**

Let's try passing an input through stdin

*)

let greet (name : string) = cloud {
    let code = """
from sys import stdin
name = stdin.readline()
print ("Hello, " + name + "!")
""" 

    return! runPythonScript code [|name|]
}

greet "F#" |> cluster.Run

(**

Let's now try a distributed workflow. 
Our goal is to use python to fetch the hostnames of every individual worker in the cluster:

*)

let getHostnamePython () = cloud {
    let code = """
import socket
print (socket.gethostname())
"""

    let! result = runPythonScript code [||]
    return result.Stdout.[0]
}

Cloud.ParallelEverywhere(getHostnamePython()) |> cluster.Run

(**

In this tutorial, you've learned how to distribute python code using clean-slate MBrace clusters.
Further features, such as timeouts, cancellation and asynchronous execution can be easily implemented
using the MBrace primitives and are left as an exercise to the reader.

Continue with further samples to learn more about the MBrace programming model.

> Note, you can use the above techniques from both scripts and compiled projects. To see the components referenced 
> by this script, see [ThespianCluster.fsx](../ThespianCluster.html) or [AzureCluster.fsx](../AzureCluster.html).
*)