module Primality

(*

Miller-Rabin probabilistic primality test and the 
Lucas-Lehmer Mersenne prime testing algorithm

*)

open System.Numerics

[<AutoOpen>]
module private Impl =

    // naive primality tester used for generating seed primes for RM
    let naivePrimeTest (n : int) =
        if n <= 1 then false
        else
            let sn = n |> float |> sqrt |> int
            seq { 2 .. sn } |> Seq.forall(fun i -> n % i <> 0)

    // enumeration of all primes
    let primes () = Seq.initInfinite ((+) 2) |> Seq.filter naivePrimeTest
    let getPrimes n = 
        primes () 
        |> Seq.take n 
        |> Seq.map (fun p -> bigint p) 
        |> Seq.toArray

    //
    //  Miller - Rabin primality test
    //

    let millerRabinTest bases n =
        // decompose n to (s,d), where n = 2^s * d, d odd
        let decompose n =
            let rec helper acc n =
                if n % 2I <> 0I then (acc,n)
                else
                    helper (acc + 1) (n / 2I)
        
            helper 0 n

        if n < 2I then false
        elif n = 2I then true
        else
            let s,d = decompose (n-1I)
        
            if s = 0 then false else

            let theTest a =
                if a < 1I || a >= n then invalidArg "a" "invalid base input" else

                if BigInteger.ModPow(a, d, n) = 1I then true
                else
                    // find r < s :  a^(2^r * d) = -1 (mod n)
                    let mutable found = false
                    let mutable r = 0
                    while not found && r < s do
                        if BigInteger.ModPow(a, (2I ** r) * d, n) = n - 1I then
                            found <- true
                        else
                            r <- r + 1
                    found
                   
            Array.forall theTest bases

    let samples = 5
    let bases = getPrimes 5
    let lastSample = bases.[samples - 1]

    // trial division; true => input is composite
    let trialDivision =
        let smallPrimes = getPrimes 20
        fun (n : bigint) -> smallPrimes |> Array.exists (fun p -> n % p = 0I)

    let smallPrimes = getPrimes 100

    // returns true => Mp composite
    // http://primes.utm.edu/notes/proofs/MerDiv.html
    let eulerTrialDivision (p : bigint) (Mp : bigint) =
        let isDivisor (q : bigint) =
            if q % p <> 1I || abs (q % 8I) <> 1I then false
            else // proceed with actual divisibility test
                Mp % q = 0I

        smallPrimes |> Array.exists isDivisor
                

    /// Lucas - Lehmer primality test for mersenne primes
    /// llt(p) true iff 2^p - 1 is prime
    let lucasLehmerTest (p : int) (Mp : bigint) =
        let mutable s = 4I
        for i = 1 to p - 2 do
            s <- (s * s - 2I) % Mp

        s = 0I


/// Miller-Rabin primality test
let isPrime (n : BigInteger) =
    if n <= lastSample then
        bases |> Array.exists ((=) n)
    else
        millerRabinTest bases n

/// Lucas - Lehmer Mersenne primality tester
/// that checks if 2^exp - 1 is prime
let isMersennePrime (exp : int) =
    if not <| isPrime (bigint exp) then false
    else
        let Mp = BigInteger.Pow(2I, exp) - 1I
        if eulerTrialDivision (bigint exp) Mp then false
        else
            lucasLehmerTest exp Mp