#load "csharp-tutorial/ThespianCluster.csx"
//#load "csharp-tutorial/AzureCluster.csx"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit AzureCluster.csx to enter your connection strings.

using System;
using System.IO;
using System.Linq;
using MBrace.Core.CSharp;
using MBrace.Flow.CSharp;

/**

# Your First 'Hello World' Computation with MBrace

> This tutorial is from the[MBrace Starter Kit](https://github.com/mbraceproject/MBrace.StarterKit).

A guide to creating a cluster is [here](http://www.mbrace.io/#try).

Start C# Interactive in Visual Studio [navigate to View/Other Windows/C# Interactive].
In order to execute a piece of code in the REPL, copy and paste it to the interactive window.
The command below connects to the cluster; if you are using a locally simulated cluster it also creates the cluster.

*/

var cluster = Config.GetCluster();

/**

Next, get details of the workers in your cluster.Again, highlight the text below and
execute it in your scripting client:

*/

cluster.ShowWorkers();

/** Now execute your first cloud workflow, returning a handle to the running job: */

var workflow = CloudBuilder.FromFunc(() => "Hello, World!");
var task = cluster.CreateProcess(workflow);

/**

This submits a task to the cluster.To get details for the task, execute the
following in your scripting client: 

*/

task.ShowInfo();

/**

Your task is likely complete by now.To get the result returned by your
task, execute the following in your scripting client: 
    
*/

var text = task.Result;

/**

Alternatively we can do this all in one line: 
    
*/

var quickText = cluster.Run(CloudBuilder.FromFunc(() => "Hello, World!"));

/**

To check that you are running in the cloud, compare a workflow running locally
with one using cloud execution. (Note, if using an MBrace.Thespian locally simulated
cluster, these will be identical.) 
    
*/

var testWorkflow =
    CloudBuilder
        .FromFunc(() => Console.WriteLine("Hello, World!"))
        .OnSuccess(unit => Environment.MachineName);

var localResult = cluster.RunLocally(testWorkflow);
var remoteResult = cluster.Run(testWorkflow);

/**

## Controlling the Cluster

To view the history of processes, execute the following line from your script

*/

cluster.ShowProcesses();

/**

In case you run into trouble, you can clear all process records in the cluster
by executing the following from your scripting client:

*/

cluster.ClearAllProcesses();