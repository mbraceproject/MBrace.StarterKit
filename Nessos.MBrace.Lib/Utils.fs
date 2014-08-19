namespace Nessos.MBrace.Lib
open System.IO

    [<AutoOpen>]
    module Utils =
        type Async with
            static member AwaitTask (task : System.Threading.Tasks.Task) : Async<unit> =
                task.ContinueWith (ignore) |> Async.AwaitTask

        let asyncCopyTo (source : Stream, dest : Stream) : Async<unit> =
            Async.AwaitTask(source.CopyToAsync(dest))

        [<RequireQualifiedAccess>]
        module List =
            /// split list at given length
            let splitAt n (xs : 'a list) =
                let rec splitter n (left : 'a list) right =
                    match n, right with
                    | 0 , _ | _ , [] -> List.rev left, right
                    | n , h :: right' -> splitter (n-1) (h::left) right'

                splitter n [] xs

            /// split list in half
            let split (xs : 'a list) = splitAt (xs.Length / 2) xs

        [<RequireQualifiedAccess>]
        module Array =
            /// split array in half
            let split (data : 'T []) : ('T [] * 'T []) =
                let half = data.Length / 2
                (data |> Seq.take half |> Seq.toArray, data |> Seq.skip half |> Seq.toArray)


            /// split array into given number of segments
            let rec segment slices (input : 'T []) =
                if slices < 0 then invalidArg "invalid segmentation factor." "slices"
                elif slices > input.Length then segment input.Length input
                else
                    let size = input.Length / slices
                    let offset = input.Length % slices
                    [| 
                        for i in 0 .. slices - 1 do
                            if i < offset then
                                yield input.[i * (size + 1) .. (i + 1) * (size + 1) - 1]
                            else
                                yield input.[i * size + offset .. (i + 1) * size + offset - 1]
                    |]
            
            /// partition array in segments of given size
            let rec partition size (input : 'T []) =
                let q, r = input.Length / size , input.Length % size
                [|
                    for i in 0 .. q-1 do
                        yield input.[ i * size .. (i + 1) * size - 1]

                    if r > 0 then yield input.[q * size .. ]
                |]