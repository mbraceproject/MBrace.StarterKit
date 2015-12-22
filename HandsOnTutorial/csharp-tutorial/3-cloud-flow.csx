#load "csharp-tutorial/ThespianCluster.csx"
//#load "csharp-tutorial/AzureCluster.csx"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit AzureCluster.csx to enter your connection strings.

// IMPORTANT:
// Before running this tutorial, make sure that you install binding redirects to FSharp.Core
// on your C# Interactive executable. To do this, find "InteractiveHost.exe" in your Visual Studio 2015 Installation.
// From your command prompt, enter the following command:
//
//   explorer.exe /select, %programfiles(x86)%\Microsoft Visual Studio 14.0\Common7\IDE\PrivateAssemblies\InteractiveHost.exe
//
// Find the file "InteractiveHost.exe.config" that is bundled in the C# tutorial folder and place it next to InteractiveHost.exe.
// Now, reset your C# Interactive session. You are now ready to start using your C# REPL.

using System;
using System.IO;
using System.Linq;
using MBrace.Core.CSharp;
using MBrace.Flow.CSharp;

// Initialize client object to an MBrace cluster
var cluster = Config.GetCluster();

/**

# Introduction to Data Parallel Cloud Flows

> This tutorial is from the[MBrace Starter Kit](https://github.com/mbraceproject/MBrace.StarterKit).

You now learn the CloudFlow programming model, for cloud - scheduled
parallel data flow tasks. This model is similar to Hadoop and Spark.

CloudFlow.OfArray partitions the input array based on the number of
available workers. The parts of the array are then fed into cloud tasks
implementing the map and filter stages. The final 'CountBy' stage is
implemented by a final cloud task.

*/

var inputs = Enumerable.Range(1, 100).ToArray();

var streamComputationWorkflow = CloudFlow
    .OfArray(inputs)
    .Select(num => num * num)
    .Select(num => num % 10)
    .CountBy(num => num)
    .ToArray();

var streamComputationTask = cluster.CreateProcess(streamComputationWorkflow);

/**
Next, check the progress of your job.

> Note: the number of cloud tasks involved, which should be the number of workers * 2.This indicates
> the input array has been partitioned and the work carried out in a distributed way.
*/

streamComputationTask.ShowInfo();

/**

Next, await the result 
    
*/

streamComputationTask.Result;

/**

Data parallel cloud flows can be used for all sorts of things.
Later, you will see how to source the inputs to the data flow from
a collection of cloud files, or from a partitioned cloud vector.


## Persisting intermediate results to cloud storage

Results of a flow computation can be persisted to store by terminating
with a call to CloudFlow.persist / persistaCached.
This creates a PersistedCloudFlow instance that can be reused without
performing recomputations of the original flow.

*/

var persistedCloudFlow =
    cluster.Run(
        CloudFlow
            .OfArray(Enumerable.Range(1, 100).ToArray())
            .SelectMany(i => Enumerable.Range(1, 10000).Select(j => new Tuple<int, string>(i + j, j.ToString())))
            .Persist(MBrace.Core.StorageLevel.Memory));


persistedCloudFlow.ShowInfo();

var length = persistedCloudFlow.Count;
var max = cluster.Run(persistedCloudFlow.MaxBy((Tuple<int, string> t) => t.Item1));

/**

## Computing house pricing data

In this example we will be using CloudFlow to extract knowledge from a dataset
hosted in a public web server. In this example, we will be using the UK land registry's
public housing market trend data which is encoded in CSV files:

*/

var urls = new string[] {
    "http://publicdata.landregistry.gov.uk/market-trend-data/price-paid-data/a/pp-2014.csv",
    "http://publicdata.landregistry.gov.uk/market-trend-data/price-paid-data/a/pp-2015.csv" };

/**

We will be using CloudFlow to parallel-download our dataset across the cluster and parse the CSV
files per line. We will be parsing the dataset using anonymous types and persist the parsed data
across the cluster by caching our CloudFlow:

*/


// helper method: trims quote literals from given string
string trim(string input) { return input.Trim(new char[] { '\"' }); }

var cacheFlow = CloudFlow
    .OfHttpFileByLine(urls) // read CSV dataset by text line
    .Select(line => line.Split(',')) // split csv line into tokens
    .Select(arr => // parse using anonymous types
        new {
            TransactionId = Guid.Parse(trim(arr[0])), Price = Double.Parse(trim(arr[1])), DateOfTransfer = DateTime.Parse(trim(arr[2])),
            PostCode = trim(arr[3]), Street = trim(arr[10]), District = trim(arr[13]), City = trim(arr[12]), County = trim(arr[14])
        }) 
    .Cache(); // cache to memory across cluster

var cacheFlowProc = cluster.CreateProcess(cacheFlow); // Start caching process

cacheFlowProc.ShowInfo(); // track download-parse-cache workflow

var cachedFlow = cacheFlowProc.Result; // get the completed cached dataset

/**

Let's now use our cached flow to extract knowledge from out dataset.
First, let's find the top 10 properties found in the City of London:

*/

var top10London = cachedFlow
    .Where(trans => trans.City == "LONDON")
    .OrderByDescending(trans => trans.Price, 10)
    .ToArray();

cluster.Run(top10London);

/**

Now, let's compute find the 10 cities with the highest property prices, on average:

*/

var top10AvgCities = cachedFlow
    .AverageByKey((row => row.City), (row => row.Price))
    .OrderByDescending((kv => kv.Value), 10)
    .ToArray();

cluster.Run(top10AvgCities);

/**

## Summary

In this tutorial, you've learned the basics of the CloudFlow programming
model, a powerful data - flow model for scalable pipelines of data.
  
Continue with further samples to learn more about the MBrace programming model.
  
*/