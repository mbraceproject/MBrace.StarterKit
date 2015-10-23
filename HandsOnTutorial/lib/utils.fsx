[<AutoOpen>]
module Utils

/// Creates a new HashSet with provided sequence
let hashSet (ts : seq<'T>) = new System.Collections.Generic.HashSet<'T>(ts)

module Array = 
    let chunkBySize (n:int) (numbers: 'T[])  =
        if n <= 0 then invalidArg "n" "must be positive."

        [| for i in 1 .. numbers.Length / n  do 
            yield [| for j in ((i-1) * n) .. (i * n - 1) do 
                       yield numbers.[j] |] 
           if numbers.Length % n <> 0 then 
            yield [| for j in (numbers.Length / n) * n .. numbers.Length - 1 do 
                       yield numbers.[j] |] |] 

    let splitInto (n:int) (numbers: 'T[])  = 
        if n <= 0 then invalidArg "n" "must be positive."
        if numbers.Length < n then 
            numbers |> Array.map (fun t -> [| t |]) 
        else
            chunkBySize (numbers.Length / n) numbers

module List = 
    let chunkBySize (n:int) (numbers: 'T list)  =  numbers |> List.toArray |> Array.chunkBySize n |> Array.toList |> List.map Array.toList
    let splitInto (n:int) (numbers: 'T list)  =  numbers |> List.toArray |> Array.splitInto n |> Array.toList |> List.map Array.toList
