#load "../../packages/MBrace.Runtime.0.5.4-alpha/bootstrap.fsx" 

open Nessos.MBrace
open Nessos.MBrace.Client
open Nessos.MBrace.Store
open Nessos.MBrace.Lib

//
//  example : thumbnail creation
//  The purpose of this demo is to demonstrate the usage of the CloudFiles.
//  The idea is to make the StoreProvider point to an existing directory containing
//  your existing files. This way your files are visible as cloud files.

#r "../../bin/Demo.Lib.dll"

open System
open System.IO
open System.Drawing
open System.Drawing.Drawing2D

open Demo.Lib

/// creates an 128x128 thumbnail out of given file
let create (bytes : byte []) = 
        let ms = new MemoryStream()
        ms.Write(bytes, 0, bytes.Length)
        ms.Position <- 0L
        let orig = new Bitmap(ms)
        let width, height = 
            if (orig.Width > orig.Height)
            then 128, 128 * orig.Height / orig.Width
            else 128 * orig.Width / orig.Height, 128
        let thumb = new Bitmap(width, height)
        let graphic = Graphics.FromImage(thumb, 
                                            InterpolationMode = InterpolationMode.HighQualityBicubic,
                                            SmoothingMode = SmoothingMode.AntiAlias,
                                            PixelOffsetMode = PixelOffsetMode.HighQuality)
        do graphic.DrawImage(orig, 0, 0, width, height)
        let dest = new MemoryStream()
        do thumb.Save(dest, System.Drawing.Imaging.ImageFormat.Jpeg)
        graphic.Dispose()
        dest.Position <- 0L
        dest

[<Cloud>]
let createThumbnail (file : ICloudFile) = cloud {
        let! bytes = CloudFile.ReadAllBytes(file)
        return! CloudFile.New("Thumbs", file.Name, (fun ds -> let s = create bytes in Stream.AsyncCopy(s, ds) ))
    }

[<Cloud>]
let createThumbnails () = 
    cloud {
        // list all files
        let! sourceImages = CloudFile.Enumerate "Images"

        return!
            sourceImages 
            |> Array.map (fun f -> cloud { return! createThumbnail f })
            |> Cloud.Parallel
    }

let path = Path.Combine(__SOURCE_DIRECTORY__ ,  "../../data/Thumbnails")
let sourceDir = Path.Combine(path, "Images")
let thumbDir = Path.Combine(path, "Thumbs")

/// the mbrace store now points to the sourceDir and will be
/// able to use the images as CloudFiles (See the CreateThumbnail function).
// This is a client settings.
MBraceSettings.DefaultStore <- FileSystemStore.Create path

// In order for the StoreProvider setting to be used by the
// runtime you need to either change the mbraced.exe configuration and restart
// or just kill you current runtime and initialize a new one.
// the new store configuration will be used by the new runtime.
let runtime = MBrace.InitLocal(totalNodes = 4)

let ps = runtime.CreateProcess <@ createThumbnails () @>

runtime.ShowProcessInfo ()

let thumbs = ps.AwaitResult()

// all done
// go to to the `thumbDir` and see the thumbnails