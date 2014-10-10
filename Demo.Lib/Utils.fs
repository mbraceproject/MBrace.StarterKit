namespace Demo.Lib

    [<AutoOpen>]
    module Utils =

        open System
        open System.IO

        type Async with
            static member AwaitTask (task : System.Threading.Tasks.Task) : Async<unit> =
                task.ContinueWith ignore |> Async.AwaitTask

        [<RequireQualifiedAccess>]
        module CloudArray =
            
            /// <summary>
            ///     Composes a collection of distributed cloud arrays into one
            /// </summary>
            /// <param name="ts"></param>
            let inline concat< ^CloudArray when ^CloudArray : (member Append : ^CloudArray -> ^CloudArray)> (ts : seq< ^CloudArray>) =
                Seq.reduce (fun t t' -> ( ^CloudArray : (member Append : ^CloudArray -> ^CloudArray) (t, t'))) ts

        [<RequireQualifiedAccess>]
        module List =

            /// <summary>
            ///     split list at given length
            /// </summary>
            /// <param name="n">splitting point.</param>
            /// <param name="xs">input list.</param>
            let splitAt n (xs : 'a list) =
                let rec splitter n (left : 'a list) right =
                    match n, right with
                    | 0 , _ | _ , [] -> List.rev left, right
                    | n , h :: right' -> splitter (n-1) (h::left) right'

                splitter n [] xs

            /// <summary>
            ///     split list in half
            /// </summary>
            /// <param name="xs">input list</param>
            let split (xs : 'a list) = splitAt (xs.Length / 2) xs

        [<RequireQualifiedAccess>]
        module Array =

            /// <summary>
            ///     partitions an array into chunks of given size
            /// </summary>
            /// <param name="chunkSize">chunk size.</param>
            /// <param name="input">Input array.</param>
            let partition chunkSize (input : 'T []) =
                let q, r = input.Length / chunkSize , input.Length % chunkSize
                [|
                    for i in 0 .. q-1 do
                        yield input.[ i * chunkSize .. (i + 1) * chunkSize - 1]

                    if r > 0 then yield input.[q * chunkSize .. ]
                |]

        type Stream with
            static member AsyncCopy (source : Stream, dest : Stream) : Async<unit> =
                Async.AwaitTask(source.CopyToAsync(dest))