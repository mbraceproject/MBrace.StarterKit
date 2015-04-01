#load "credentials.fsx"

open System
open System.IO
open MBrace
open MBrace.Azure
open MBrace.Azure.Client
open MBrace.Workflows
open MBrace.Flow

(**
 This demo shows how to send a simple computation to an mbrace cluster

 A guide to creating the cluster is here: https://github.com/elastacloud/mbrace-on-brisk-starter/

 Before you create your cluster you will need an Azure account and an Azure Cloud Storage connection.

 Before running, edit credentials.fsx to enter your connection strings.

 **)

// First connect to the cluster using a configuration to bind to your storage and service bus on Azure.
//
// Before running, edit credentials.fsx to enter your connection strings.
let cluster = Runtime.GetHandle(config)

// attach console logger to client object
//cluster.AttachClientLogger(new ConsoleLogger())

// We can connect to the cluster and get details of the workers in the pool etc.
cluster.ShowWorkers()

// Attach a local worker to the cluster state
//cluster.AttachLocalWorker()

// We can view the history of processes
cluster.ShowProcesses()

// Execute a cloud workflow and get a handle to the running job
let job = 
    cloud { return "Hello world!" } 
    |> cluster.CreateProcess

// You can evaluate helloWorldProcess to get details on it
let isJobComplete = job.Completed

// Block until the result is computed by the cluster
let text = job.AwaitResult()

// Alternatively we can do this all in one line
let quickText = 
    cloud { return "Hello world!" } 
    |> cluster.Run

// This can be used to clear all process records in the cluster
//
// cluster.ClearAllProcesses()

// If you need to get really heavy, you can reset the cluster, which clears 
// all process state in queues and storage. Other storage is left unchanged.
// Your worker roles may need to be manually rebooted (e.g. from the Azure 
// management console).
//
// cluster.Reset()


