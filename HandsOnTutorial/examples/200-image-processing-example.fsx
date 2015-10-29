(*** hide ***)
#load "../ThespianCluster.fsx"
//#load "../AzureCluster.fsx"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit AzureCluster.fsx to enter your connection strings.

open System
open System.IO
open MBrace.Core
open MBrace.Flow

// Initialize client object to an MBrace cluster
let cluster = Config.GetCluster() 

(**
# Example: Using MBrace for image processing

> This tutorial is from the [MBrace Starter Kit](https://github.com/mbraceproject/MBrace.StarterKit).

In this tutorial, you use the AForge (you can install AForge from Nuget) to turn color images into gray ones by applying a gray filter.

*)

#r "../../packages/AForge/lib/AForge.dll" 
#r "../../packages/AForge.Math/lib/AForge.Math.dll" 
#r "../../packages/AForge.Imaging/lib/AForge.Imaging.dll" 

open System.Drawing
open AForge.Imaging.Filters
open System.Net
open System.IO

(** Next, you define a method to download an image from a url, and return a stream containing the downloaded image. *)
let GetStreamFromUrl (url : string) =
    let webClient = new WebClient()
    let data = webClient.DownloadData(url)
    let stream = new MemoryStream(data)
    stream


(** Next, you create a method that turns the downloaded color image into a gray image by applying an AForge filter, and then uploads the gray image to cloud storage. *)
let GrayImageFromWeb (url : string) file =
    cloud {
        // Download image.
        let inputStream =GetStreamFromUrl url
        // Apply the gray filter to the original image.
        let originalBmp = new Bitmap(inputStream)
        let grayedBmp = Grayscale.CommonAlgorithms.BT709.Apply originalBmp        
        // Get the grayed image.
        let outputStream = new MemoryStream()
        do grayedBmp.Save(outputStream, Imaging.ImageFormat.Bmp)        
        // Upload the gray image to cloud storage.
        let! cFile = CloudFile.WriteAllBytes(file, outputStream.ToArray())
        inputStream.Close()
        outputStream.Close()
        return cFile
    }


(** Last, you perform parallel downloading and image processing in the MBrace cluster. *)
let urls = [|
    "https://upload.wikimedia.org/wikipedia/commons/thumb/5/54/Tigress_at_Jim_Corbett_National_Park.jpg/330px-Tigress_at_Jim_Corbett_National_Park.jpg";
    "https://upload.wikimedia.org/wikipedia/commons/thumb/8/8c/Poligraf_Poligrafovich.JPG/800px-Poligraf_Poligrafovich.JPG" 
    "https://upload.wikimedia.org/wikipedia/commons/e/eb/Denizli_Atat%C3%BCrk_Stadyumu.jpg"
    |]

let tasks = 
    [|for url in urls -> GrayImageFromWeb url (sprintf ("tmp/%s") (Path.GetFileName(Uri(url).LocalPath)))  |> cluster.CreateProcess |] 

(** 
The results are a set of cloud storage file paths for the generated images.  You can look these up in your
cloud storage account browser.

**)

let results = 
    [| for t in tasks -> t.Result |]


(** 
In this tutorial, you've learned how to use the AForge image processing library to turn color images to gray ones in MBrace.
Continue with further samples to learn more about the MBrace programming model.  

> Note, you can use the above techniques from both scripts and compiled projects. To see the components referenced 
> by this script, see [MBrace.Thespian.fsx](MBrace.Thespian.html) or [MBrace.Azure.fsx](MBrace.Azure.html).
*)
