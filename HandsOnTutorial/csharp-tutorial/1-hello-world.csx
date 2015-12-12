#load "csharp-tutorial/ThespianCluster.csx"
//#load "csharp-tutorial/AzureCluster.csx"

using MBrace.Core.CSharp;
using MBrace.Flow.CSharp;

var cluster = Config.GetCluster();

var helloComp = CloudBuilder.FromFunc(() => "Hello, World!");

cluster.Run(helloComp)