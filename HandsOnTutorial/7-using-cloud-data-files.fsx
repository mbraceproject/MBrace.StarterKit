(*** hide ***)
#load "ThespianCluster.fsx"
//#load "AzureCluster.fsx"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit AzureCluster.fsx to enter your connection strings.

open System
open System.IO
open MBrace.Core
open MBrace.Flow

// Initialize client object to an MBrace cluster
let cluster = Config.GetCluster() 

(**
# Creating and Using Cloud Files

MBrace clusters have a cloud file system mapped to the corresponding cloud fabric. This can be 
used like a distributed file system such as HDFS.

## Accessing the Cloud File System from F# scripts

First let's define and use some Unix-like file functions to access the cloud file system 
from your F# client script. (Using these is optional: you can also use the MBrace API directly).

*)

let root = CloudPath.DefaultDirectory |> cluster.RunLocally

let (++) path1 path2 = CloudPath.Combine( [| path1; path2 |] ) |> cluster.RunLocally

let ls path = cluster.Store.File.Enumerate(path)

let rec lsRec path = 
    seq { yield! cluster.Store.File.Enumerate(path)
          for d in cluster.Store.Directory.Enumerate(path) do 
              yield! lsRec path }

let mkdir path = cluster.Store.Directory.Create(path)

let rmdir path = cluster.Store.Directory.Delete(path)

let rmdirRec path = cluster.Store.Directory.Delete(path,recursiveDelete=true)

let randdir() = cluster.Store.Path.GetRandomDirectoryName()

let randfile() = cluster.Store.Path.GetRandomFilePath()

let rm path = CloudFile.Delete(path) |> cluster.RunLocally

let cat path = CloudFile.ReadAllText(path) |> cluster.RunLocally

let catLines path = CloudFile.ReadAllLines(path) |> cluster.RunLocally

let catBytes path = CloudFile.ReadAllBytes(path) |> cluster.RunLocally

let write path text = CloudFile.WriteAllText(path, text) |> cluster.RunLocally

let writeLines path lines = CloudFile.WriteAllLines(path, lines) |> cluster.RunLocally

let writeBytes path bytes = CloudFile.WriteAllBytes(path, bytes) |> cluster.RunLocally


(**
You now use these functions to create directories and files:

*)

mkdir (root ++ "data")

write (root ++ "data" ++ "humpty.txt") "All the king's horses and all the king's men"
write (root ++ "data" ++ "spider.txt") "Incy wincy spider climed up the water spout"

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

First, crete a local temp file. 
*)
let tmpFile = 
    let path = Path.GetTempFileName()
    let lines = 
        [ for i in 1 .. 1000 do 
             let time = DateTime.Now.Date.AddSeconds(float i)
             let text = sprintf "click user%d %s" (i%10) (time.ToString())
             yield text ]
    File.WriteAllLines(path, lines)
    path

(** Next, you upload the created file to the tmp container in cloud storage. The tmp container
will be created if it does not exist. Note the use of the local {...} expression and the cluster.RunLocally method:  
the uploading has to be run locally because it accesses a local path. *)
let cFile = 
    local {
        return! CloudFile.Upload(tmpFile, sprintf "tmp/%s" (Path.GetFileName tmpFile))     
    }    
    |> cluster.RunLocally

(** After uploading the file, you remove the local file. *)
File.Delete tmpFile

(** Now process the file in the MBrace cluster. This cloud expression runs in the MBrace cluster. *)
let lines = 
    cloud {
        let! lines = CloudFile.ReadAllLines cFile.Path
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
        cloud { 
            let lines = 
                [ for j in 1 .. 100000 -> 
                    "file " + string i + ", item " + string (i*100+j) + ", " + string (j+i*100) ] 
            let nm = dataDir + "/file" + string i
            do! CloudFile.Delete(nm)
            let! file = CloudFile.WriteAllLines(nm, lines)
            return file.Path
        } ]
   |> Cloud.Parallel 
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
> by this script, see [MBrace.Thespian.fsx](MBrace.Thespian.html) or [MBrace.Azure.fsx](MBrace.Azure.html).
 *)

