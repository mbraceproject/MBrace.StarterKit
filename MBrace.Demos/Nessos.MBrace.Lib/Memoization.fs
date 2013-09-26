namespace Nessos.MBrace.Lib

open Nessos.MBrace.Client

[<Cloud>]
[<RequireQualifiedAccessAttribute>]
module Memoization = 
    
    /// <summary>Memoize the given function using the StoreProvider and
    /// MutableCloudRefs as a lookup.</summary>
    /// <param name="cacheName"> The container name to be used by the StoreProvider.</param>
    /// <param name="encode"> The function that maps the function's domain to valid Store filenames.</param>
    /// <param name="f"> The function to memoize.</param>
    /// <returns> The function that uses memoization.</returns>
    let memoize (cacheName : string) (encode : 'a -> string) 
                (f : 'a -> ICloud<'b>) : ('a -> ICloud<'b>) = 
        fun a ->
            cloud {
                let! b = MutableCloudRef.TryGet<'b>(cacheName, encode a)
                match b with
                | None ->
                    let! v  = f a
                    let! r = MutableCloudRef.TryNew(cacheName, encode a, v)
                    return v
                | Some b -> return! MutableCloudRef.Read(b)
            }
