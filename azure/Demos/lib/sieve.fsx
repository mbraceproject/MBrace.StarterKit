module Sieve 


// Implements the Sieve of Eratosthenes
let getPrimes nmax =
    let sieve = new System.Collections.BitArray((nmax/2) + 1, true)
    let result = new ResizeArray<int>(nmax / 10)
    let upper = int (sqrt (float nmax))   
    
    if nmax > 1 then result.Add(2) 

    let mutable m = 1
    while 2 * m + 1 <= nmax do
       if sieve.[m] then
           let n = 2 * m + 1
           if n <= upper then 
               let mutable i = m
               while 2 * i < nmax do sieve.[i] <- false; i <- i + n
           result.Add n
       m <- m + 1
    
    result |> Seq.toArray
