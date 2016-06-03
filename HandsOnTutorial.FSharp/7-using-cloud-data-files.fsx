(*** hide ***)
#load "ThespianCluster.fsx"
//#load "AzureCluster.fsx"
//#load "AwsCluster.fsx"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit AzureCluster.fsx to enter your connection strings.

open System
open System.IO
open MBrace.Core
open MBrace.Flow

// Initialize client object to an MBrace cluster
let cluster = Config.GetCluster() 
#load "lib/utils.fsx"

(**
# Creating and Using Cloud Files

> This tutorial is from the [MBrace Starter Kit](https://github.com/mbraceproject/MBrace.StarterKit).

MBrace clusters have a cloud file system mapped to the corresponding cloud fabric. This can be 
used like a distributed file system such as HDFS.

## Accessing the Cloud File System from F# scripts

First let's define and use some Unix-like file functions to access the cloud file system 
from your F# client script. (Using these is optional: you can also use the MBrace API directly).

*)

let fs = cluster.Store.CloudFileSystem

let root = fs.Path.DefaultDirectory

let (++) path1 path2 = fs.Path.Combine(path1, path2)

let ls path = fs.File.Enumerate(path)

let rec lsRec path = 
    seq { yield! fs.File.Enumerate(path)
          for d in fs.Directory.Enumerate(path) do 
              yield! lsRec path }

let mkdir path = fs.Directory.Create(path)

let rmdir path = fs.Directory.Delete(path)

let rmdirRec path = fs.Directory.Delete(path,recursiveDelete=true)

let randdir() = fs.Path.GetRandomDirectoryName()

let randfile() = fs.Path.GetRandomFilePath()

let rm path = fs.File.Delete path

let cat path = fs.File.ReadAllText path

let catLines path = fs.File.ReadAllLines path

let catBytes path = fs.File.ReadAllBytes path

let write path text = fs.File.WriteAllText(path, text)

let writeLines path lines = fs.File.WriteAllLines(path, lines)

let writeBytes path bytes = fs.File.WriteAllBytes(path, bytes)


(**
You now use these functions to create directories and files:

*)

mkdir (root ++ "data")

write (root ++ "data" ++ "humpty.txt") "All the king's horses and all the king's men"
writeLines (root ++ "data" ++ "spider.txt") [ for i in 0 .. 1000 -> "Incy wincy spider climed up the water spout" ]

ls (root ++ "data")

(** Now check you've created the files correctly: *)
cat (root ++ "data" ++ "spider.txt") 
catLines (root ++ "data" ++ "spider.txt") 

(** Now remove the directory of data: *)

rmdirRec (root ++ "data")


(**
## Progammatic upload of data as part of cloud workflows

The Unix-like abbreviations from the previous section are for use from your client scripts.
You can also use the MBrace cloud file API directly from cloud workflows.

First, create a local temp file. 
*)
let localTmpFile = 
    let path = Path.GetTempFileName()
    let lines = 
        [ for i in 1 .. 1000 do 
             let time = DateTime.Now.Date.AddSeconds(float i)
             let text = sprintf "click user%d %s" (i%10) (time.ToString())
             yield text ]
    File.WriteAllLines(path, lines)
    path

(** Next, you upload the created file to the tmp container in cloud storage. The tmp container
will be created if it does not exist.  *)
let cloudFile = fs.File.Upload(localTmpFile, sprintf "%s/tmp/%s" root (Path.GetFileName localTmpFile))     

(** After uploading the file, you remove the local file. *)
File.Delete localTmpFile

(** Now process the file in the MBrace cluster. This cloud expression runs in the MBrace cluster. *)
let lines = 
    cloud {
        let lines = fs.File.ReadAllLines cloudFile.Path
        let users = [ for line in lines -> line.Split(' ').[1] ]
        return users |> Seq.distinct |> Seq.toList
    } 
    |> cluster.Run

(** 
## Using multiple cloud files as input to distributed cloud flows

Processing one small file in the cloud is not of much use.  However multiple, large cloud files can 
be used as inputs to distributed cloud flows in a similar way to map-reduce jobs in Hadoop.

Next you generate a collection of 100 cloud files and process them using a distributed cloud flow. 
*)
let dataDir = root ++ "data"

mkdir dataDir

let cloudFiles = 
    [ for i in 1 .. 100 ->
        local { 
            let lines = 
                [ for j in 1 .. 100000 -> 
                    "file " + string i + ", item " + string (i*100+j) + ", " + string (j+i*100) ] 
            let nm = dataDir + "/file" + string i
            do! CloudFile.Delete(nm)
            let file = fs.File.WriteAllLines(nm, lines)
            return file.Path
        } ]
   |> Cloud.ParallelBalanced
   |> cluster.Run


(** A collection of cloud files can be used as input to a cloud parallel data flow, summing 
the third column of each line of each file in a distributed way. *)
let sumOfLengthsOfLines =
    cloudFiles
    |> CloudFlow.OfCloudFileByLine
    |> CloudFlow.map (fun line -> line.Split(',').[2] |> int)
    |> CloudFlow.sum
    |> cluster.Run

(** Cleanup the cloud data *)
rmdirRec (root ++ "data")

(** 
## Summary

In this tutorial, you've learned how to use cloud files, from some simple Unix-like
operations to using multiple cloud files as partitioned inputs into CloudFlow programming.
Continue with further samples to learn more about the MBrace programming model.  



> Note, you can use the above techniques from both scripts and compiled projects. To see the components referenced 
> by this script, see [ThespianCluster.fsx](ThespianCluster.html) or [AzureCluster.fsx](AzureCluster.html).
 *)

