namespace Nessos.MBrace.Lib


[<AutoOpen>]
module CloudFileExtensions =
    open System
    open System.IO
    open System.Text
    open System.Collections
    open System.Collections.Generic
    open System.Runtime.Serialization

    open Nessos.MBrace.Client

    let private asyncWriteLine (source : StreamWriter, line : string) : Async<unit> =
        Async.AwaitTask(source.WriteLineAsync(line))

    let private asyncWriteText (source : StreamWriter, text : string) : Async<unit> =
        Async.AwaitTask(source.WriteAsync(text))

    let private asyncWriteBytes (source : Stream, buffer : byte[], offset : int, count : int) : Async<unit> =
        Async.AwaitTask(source.WriteAsync(buffer, offset, count))


    type CloudFile with
        
        [<Cloud>]
        static member ReadLines(file : ICloudFile, ?encoding : Encoding) =
            cloud {
                let reader (stream : Stream) = async {
                    let s = seq {
                        use sr = 
                            match encoding with
                            | None -> new StreamReader(stream)
                            | Some e -> new StreamReader(stream, e)
                        while not sr.EndOfStream do
                            yield sr.ReadLine()
                    }
                    return s
                }
                return! CloudFile.ReadSeq(file, reader)
            }

        [<Cloud>]
        static member WriteLines(container : string, name : string, lines : seq<string>, ?encoding : Encoding) =
            cloud {
                let writer (stream : Stream) = async {
                    use sw = 
                        match encoding with
                        | None -> new StreamWriter(stream)
                        | Some e -> new StreamWriter(stream, e)
                    for line in lines do
                        do! asyncWriteLine(sw, line)
                }
                return! CloudFile.Create(container, name, writer)
            }

        [<Cloud>]
        static member ReadAllText(file : ICloudFile, ?encoding : Encoding) =
            cloud {
                let reader (stream : Stream) = async {
                    use sr = 
                        match encoding with
                        | None -> new StreamReader(stream)
                        | Some e -> new StreamReader(stream, e)
                    return sr.ReadToEnd()
                }
                return! CloudFile.Read(file, reader)
            }

        [<Cloud>]
        static member WriteAllText(container : string, name : string, text : string, ?encoding : Encoding) =
            cloud {
                let writer (stream : Stream) = async {
                    use sw = 
                        match encoding with
                        | None -> new StreamWriter(stream)
                        | Some e -> new StreamWriter(stream, e)
                    do! asyncWriteText(sw, text)
                }
                return! CloudFile.Create(container, name, writer)
            }
        
        [<Cloud>]
        static member ReadAllBytes(file : ICloudFile) =
            cloud {
                let reader (stream : Stream) = async {
                    use ms = new MemoryStream()
                    do! asyncCopyTo(stream, ms)
                    return ms.ToArray()
                }
                return! CloudFile.Read(file, reader)
            }

        [<Cloud>]
        static member WriteAllBytes(container : string, name : string, buffer : byte []) =
            cloud {
                let writer (stream : Stream) = async {
                    do! asyncWriteBytes(stream, buffer, 0, buffer.Length)
                }
                
                return! CloudFile.Create(container, name, writer)
            }
