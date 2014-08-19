namespace Demo.Lib

    [<AutoOpen>]
    module Utils =

        open System
        open System.IO

        type Async with
            static member AwaitTask (task : System.Threading.Tasks.Task) : Async<unit> =
                task.ContinueWith ignore |> Async.AwaitTask

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
            /// partitions an array into chunks of given size
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