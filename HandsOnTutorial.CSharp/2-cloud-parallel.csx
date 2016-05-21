#load "config/ThespianCluster.csx"
//#load "config/AzureCluster.csx"
//#load "config/AwsCluster.csx"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit AzureCluster.csx to enter your connection strings.

// IMPORTANT:
// Before running this tutorial, make sure that you install binding redirects to FSharp.Core
// on your C# Interactive executable. To do this, find "InteractiveHost.exe" in your Visual Studio 2015 Installation.
// From your command prompt, enter the following command:
//
//   explorer.exe /select, %programfiles(x86)%\Microsoft Visual Studio 14.0\Common7\IDE\PrivateAssemblies\InteractiveHost.exe
//
// Find the file "config/InteractiveHost.exe.config" that is bundled in the C# tutorial folder and place it next to InteractiveHost.exe.
// Now, reset your C# Interactive session. You are now ready to start using your C# REPL.

using System;
using System.IO;
using System.Linq;
using MBrace.Core.CSharp;
using MBrace.Flow.CSharp;

// Initialize client object to an MBrace cluster
var cluster = Config.GetCluster();

/**

# Introduction to Cloud Combinators

You now perform a very simple parallel distributed job on your MBrace cluster.

*/

/** You now use Cloud.Parallel to run 10 cloud workflows in parallel using fork-join pattern. */

var parallel = Enumerable
    .Range(1, 10)
    .Select(i => CloudBuilder.FromFunc(() => string.Format("I'm work item {0}", i)))
    .Parallel();

var parallelTask = cluster.CreateProcess(parallel);

parallelTask.ShowInfo();