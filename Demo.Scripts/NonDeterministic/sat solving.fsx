#load "../../packages/MBrace.Runtime.0.5.4-alpha/bootstrap.fsx" 

open Nessos.MBrace
open Nessos.MBrace.Client

(*
 * This is an (simple) implementation of the DPLL algorithm (SAT solver).
 * The implementation is demonstrating the usage of the Cloud.Choice combinator.
 *)

open System.IO
open System.Collections.Generic

/// Encode literals (boolean variables). Xi --> i, ~Xi --> -i
type Literal = int    
type Clause = Set<Literal>
type Cnf = Clause seq

/// Use an option type to encode (un)satisfiability.
/// If we wanted to get the formula that satisfies
/// the cnf we should use Clause option.
/// Also this way we can use the Cloud.Choice operator.
type Sat = unit option 

/// Define logical or for the Sat encoding.
let SatOr (l : Sat) (r : Sat) : Sat =
    match l, r with
    | None, None -> None
    | _ -> Some ()

///If an empty clause exists in a Cnf then the Cnf is unsatisfiable.
let hasEmptyClause (c : Cnf)= Seq.exists Seq.isEmpty c

/// Pure Literal : A literal L occurring in a set of clauses S is said to be
/// pure in S if S contains no clauses of the form ˜L ∨ C. 
/// Pure literal rule removes from a set
/// of clauses all clauses containing a pure literal.
let eliminate (cnf : Cnf) : Cnf = 
    let rec getPureLiterals (cnf : Set<Literal>) (acc : Set<Literal>) : Set<Literal> =
        if cnf.IsEmpty 
        then acc |> Set.filter (fun l -> Set.exists ((=) -l) acc |> not)
        else 
            let h = Seq.head cnf
            let t = Set.remove h cnf
            getPureLiterals t (Set.add h acc)

    let pureLit = getPureLiterals (Seq.concat cnf |> Set.ofSeq) Set.empty
    cnf |> Seq.filter (Seq.forall (fun v -> Seq.exists ((=) v) pureLit |> not))
    

let simplify (cnf : Cnf) (var : Literal) : Cnf =
    cnf
    |> Seq.filter (Set.forall ((<>) var))
    |> Seq.map    (Set.filter ((<>) -var))

/// Unit Propagation : Let S be a set of clauses. We say that a set of clauses
/// S′ is obtained from S by unit propagation if S′ is obtained from S by repeatedly performing
/// the following transformation: if S contains a unit clause,
/// i.e. a clause consisting of one literal L, then :
///     (1) remove from S every clause of the form L ∨ C′;
///     (2) replace in S every clause of the form ˜L ∨ C′ by the clause C′.
let rec propagate (cnf : Cnf) = 
    // We need an acc if we want to keep the answer

    let unit x = Seq.tryFind (Seq.length >> (=) 1) x

    match unit cnf with
    | Some s when Seq.length s = 1 -> 
        propagate (simplify cnf (Seq.exactlyOne s))
    | None     -> cnf
    | _        -> failwith "Unit propagate: invalid unit clause"


/// Choose a literal from a set of clauses. This implementation is
/// pretty naive.
let choose (clauses : Cnf) =
    try 
        let l = (Seq.head >> Seq.head) clauses
        Some(l,-l)
    with _ -> None

let rec dpll (cnf : Cnf) = 
    if hasEmptyClause cnf then false
    elif Seq.isEmpty cnf then true
    else
        let cnf = (propagate >> eliminate) cnf
        match choose cnf with
        | None -> dpll cnf
        | Some(l,nl) -> 
            dpll (simplify cnf l) || dpll (simplify cnf nl)

[<Cloud>]
let rec dpllCloud' (cnf : Cnf) : Cloud<Sat> = 
    cloud {
        let rec dpllCloud cnf d = 
            cloud {
                if hasEmptyClause cnf then return None
                elif Seq.isEmpty cnf then return Some ()
                else
                    let cnf = (propagate >> eliminate) cnf
                    match choose cnf with
                    | None -> 
                        return! dpllCloud cnf 0
                    | Some(l,nl) -> 
                        if d > 0 then
                            return! [| dpllCloud (simplify cnf l)  (d-1) 
                                       dpllCloud (simplify cnf nl) (d-1) |]
                                    |> Cloud.Choice
                        else
                            let! (a,b) = dpllCloud (simplify cnf l) 0 <.> dpllCloud (simplify cnf nl) 0
                            return SatOr a b
            }
        let! n = Cloud.GetWorkerCount()
        let m = 1 + int(ceil(log (float (n+1)) / (log 2.)))
        return! dpllCloud cnf m
    }

[<Cloud>]
let rec dpllCloud (cnf : Cnf) : Cloud<Sat> = cloud {
        if hasEmptyClause cnf then return None
        elif Seq.isEmpty cnf then return Some ()
        else
            let cnf = (propagate >> eliminate) cnf
            match choose cnf with
            | None -> 
                return! dpllCloud cnf
            | Some(l,nl) -> 
                return! [| dpllCloud (simplify cnf l)  
                           dpllCloud (simplify cnf nl) |]
                        |> Cloud.Choice
}

///Reads a file containing clauses in CNF form.
///The file's using the DIMACS format.
let readCNF filename : Cnf * int * int  = 
    let lines = File.ReadLines filename
    let header = lines |> Seq.find (fun l -> l.StartsWith "p cnf")
    let [|_ ; _ ; l ; c|] = header.Trim().Split() 
                            |> Array.filter (Seq.isEmpty >> not)
    let l , c = int l, int c // number of literals, clauses
    lines
    |> Seq.filter (fun l -> not(l.StartsWith "p" || l.StartsWith "c" || l.Length = 0))
    |> Seq.map (fun l -> l.Trim().Split() 
                          |> Array.map int 
                          |> Array.filter ((<>) 0)
                          |> Set.ofArray)
    |> List.ofSeq :> _, l , c

let runtime = MBraceRuntime.InitLocal(totalNodes = 4)

let f, l, c = readCNF (__SOURCE_DIRECTORY__ + @"\..\..\data\Sat-files\aim-50-6_0-yes1-4.cnf")

runtime.Run <@ dpllCloud f @> // None i s false, Some () is true