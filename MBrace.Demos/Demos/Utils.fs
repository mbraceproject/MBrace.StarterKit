[<AutoOpenAttribute>]
module Utils

    open System
    open System.Net
    open System.Security.Cryptography
    open System.IO

    let inline zero () : ^T = LanguagePrimitives.GenericZero< ^T>
    let inline succ (x : ^T) = x + LanguagePrimitives.GenericOne< ^T> : ^T
    let inline incr (x : ^T ref) = x := succ !x

    [<RequireQualifiedAccess>]
    module Seq =
        let inline infinite (x : ^T) =
            let rec infinite x = seq { yield x ; yield! infinite (succ x) }
            infinite x

    let downloadAsync (url : string) =
        async {
            let client = new WebClient()
            let! html = client.AsyncDownloadString(Uri url)
            return html
        }

    module Array =
        let rec partition size (input : 'T []) =
            let q, r = input.Length / size , input.Length % size
            [|
                for i in 0 .. q-1 do
                    yield input.[ i * size .. (i + 1) * size - 1]

                if r > 0 then yield input.[q * size .. ]
            |]

    type private SuccessException<'T>(value : 'T) =
        inherit Exception()
        member self.Value = value

    type Microsoft.FSharp.Control.Async with
        /// efficient raise
        static member Raise (e : #exn) : Async<'T> = Async.FromContinuations(fun (_,econt,_) -> econt e)
        /// a more functional RunSynchronously wrapper
        static member Run timeout (comp : Async<'T>) =
            match timeout with
            | None -> Async.RunSynchronously comp
            | Some t -> Async.RunSynchronously(comp, t)
        /// nondeterministic choice
        static member Choice<'T>(tasks : Async<'T option> seq) : Async<'T option> =
            let wrap task =
                async {
                    let! res = task
                    match res with
                    | None -> return ()
                    | Some r -> return! Async.Raise <| SuccessException r
                }

            async {
                try
                    do!
                        tasks
                        |> Seq.map wrap
                        |> Async.Parallel
                        |> Async.Ignore

                    return None
                with 
                | :? SuccessException<'T> as ex -> return Some ex.Value
            }


    type RandomNumberGenerator(bufsize : int) =
        let bufsize = bufsize * 4
        let buf = Array.zeroCreate<byte> bufsize
        let mutable i = 0
        let rng = new RNGCryptoServiceProvider()

        do rng.GetBytes buf

        member __.Next () =
            // buffer exhausted, refill
            if i = bufsize then
                do rng.GetBytes buf
                i <- 0

            let n = (int buf.[i]) ||| (int buf.[i+1] <<< 8) ||| (int buf.[i+2] <<< 16) ||| (int buf.[i+3] <<< 24)
            i <- i + 4
            n