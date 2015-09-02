(*** hide ***)
#load "Thespian.fsx"
#load "Azure.fsx"

open System
open System.IO
open MBrace.Core
open MBrace.Flow

// Initialize client object to an MBrace cluster:
let cluster = 
//    getAzureClient() // comment out to use an MBrace.Azure cluster; don't forget to set the proper connection strings in Azure.fsx
    initThespianCluster(4) // use a local cluster based on MBrace.Thespian; configuration can be adjusted using Thespian.fsx

(**
# Creating and Using Cloud Values and Cloud Sequences

# Using Cloud Values 

You now learn how to upload data to Azure Blob Storage using CloudValue and
then process it using MBrace cloud tasks.

When using MBrace, data is implicitly uploaded if it is
part of the closure of a cloud workflow - for example, if a value is
referenced in a cloud { ... } block.  That data is a transient part of the 
process specification.  This is often the most convenient way to get 
small amounts (KB-MB) of data to the cloud: just use the data as part
of a cloud workflow and run that work in the cloud.

If you wish to _persist_ data in the cloud - for example, if it is too big
to upload multiple times - then you can use one or more of the
cloud data constructs that MBrace provides. 
 
Note you can alternatively you use any existing cloud storage 
APIs or SDKs you already have access to. For example, if you wish you 
can read/write using the .NET Azure storage SDKs directly rather than 
using MBrace primitives.
 
You can copy larger data to Azure using the AzCopy.exe command line tool, see
https://azure.microsoft.com/en-us/documentation/articles/storage-use-azcopy/
 
You can manage storage using the "azure" command line tool, see
https://azure.microsoft.com/en-us/documentation/articles/xplat-cli/

 Before running, edit credentials.fsx to enter your connection strings.
 
*)
 
(** Here's some data (~1.0MB) *)
let data = String.replicate 10000 "The quick brown fox jumped over the lazy dog\r\n" 


(** Upload the data to blob storagand return a handle to the stored data *)
let persistedCloudData = 
    cloud { let! cell = CloudValue.New data 
            return cell }
    |> cluster.RunOnCloud

(** Run a cloud job which reads the blob and processes the data *)
let lengthOfData = 
    cloud { let! data = CloudValue.Read persistedCloudData 
            return data.Length }
    |> cluster.RunOnCloud


(** 

//# Using Cloud Arrays
//
//Next persist an array of data (each element a tuple).
//
//Here is the data we're going to upload, it's an array of 1000 tuples: *) 
//let dataGen = 
//    [| for i in 1 .. 1000 do 
//        let text = sprintf "%d quick brown foxes jumped over %d lazy dogs." i (2*i + 1)
//        yield (i, text) |]
//
//(** 
//Upload it as a CloudArray; a CloudArray is a CloudValue of a collection.
//*)
//let cloudArray = 
//    cloud {
//        let! seq = CloudValue.NewArray dataGen
//        return seq
//    } |> cluster.RunOnCloud
//
//(** For this sample we only read the 10 first elements from store: *)
//let first10 = Seq.take 10 cloudArray
//
//(** The next example runs a cloud job that aggregate all elements to an array
//and returns the results to the server: *) 
//let allData = 
//    cloud {
//        let! array = cloudArray.GetValueAsync()
//        return array
//    } |> cluster.RunOnCloud
//
//
//(** The next sample shows how a collection of cloud sequences can be used
//as input into a CloudFlow data flow. Each cloud sequence can be used as a 
//partition of data flowing into the CloudFlow.
//*)
//
//let cloudArrays = 
//    [ for i in 1 .. 10 -> 
//        cloud {
//            let! seq = CloudValue.NewArray dataGen
//            return seq
//        } ]
//    |> Cloud.Parallel
//    |> cluster.RunOnCloud
//
//let results = 
//    cloudArrays
//      |> CloudFlow.OfCloudArrays
//      |> CloudFlow.countBy (fun (k,v) -> k)
//      |> CloudFlow.toArray
//      |> cluster.RunOnCloud
//
//(** In this tutorial, you've learned how to persist data values into
//cloud storage using the `CloudValue` and `CloudSequence` types.
//Continue with further samples to learn more about the
//MBrace programming model.  *)
