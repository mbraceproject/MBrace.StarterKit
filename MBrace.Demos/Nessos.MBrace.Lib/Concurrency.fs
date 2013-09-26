namespace Nessos.MBrace.Lib.Concurrency

open Nessos.MBrace.Client

type MVar<'T> = IMutableCloudRef<'T option>

[<Cloud>]
///Implementation of the Haskell MVar, build on top of the MutableCloudRefs.
module MVar =
    /// Creates a new empty MVar.
    let newEmpty<'T> : ICloud<MVar<'T>> = MutableCloudRef.New(None)

    /// Create a new MVar containing the given value.
    let newValue<'T> value : ICloud<MVar<'T>> = MutableCloudRef.New(Some value)

    /// Puts a value in the MVar. This function will block until the MVar is empty
    /// and the put succeeds.
    let rec put (mvar : MVar<'T>) value = 
        cloud {
            let! v = MutableCloudRef.Read(mvar)
            match v with
            | None -> 
                let! ok = MutableCloudRef.Set(mvar, Some value)
                if not ok then return! put mvar value
            | Some _ ->
                return! put mvar value
        }

    /// Takes the MVar's value. This function will block until the MVar is non-empty
    /// and the take succeeds.
    let rec take (mvar : MVar<'T>) =
        cloud {
            let! v = MutableCloudRef.Read(mvar)
            match v with
            | None -> 
                return! take mvar
            | Some v -> 
                let! ok = MutableCloudRef.Set(mvar, None)
                if not ok then return! take mvar
                else return v
        }

type private Stream<'T> = MVar<Item<'T>>
and  private Item<'T> = Item of 'T * Stream<'T>

/// An implementation of a Channel using the MVar abstraction.
type Channel<'T> = private Channel of (MVar<Stream<'T>> * MVar<Stream<'T>>)

[<Cloud>]
/// Provides basic operations on the Channel type.
module Channel =

    /// Creates a new empty Channel.
    let newEmpty<'T> : ICloud<Channel<'T>> = 
        cloud {
            let! hole = MVar.newEmpty
            let! readVar = MVar.newValue hole
            let! writeVar = MVar.newValue hole
            return Channel(readVar, writeVar)
        }

    /// Writes a value to a Channel.
    let write<'T> (chan : Channel<'T>) (value : 'T) : ICloud<unit> =
        cloud {
            let (Channel(_, writeVar)) = chan
            let! newHole = MVar.newEmpty
            let! oldHole = MVar.take writeVar
            do! MVar.put writeVar newHole
            do! MVar.put oldHole (Item(value, newHole))
        }

    /// Reads a value from a Channel.
    let read<'T> (chan : Channel<'T>) : ICloud<'T> = 
        cloud {
            let (Channel(readVar,_)) = chan
            let! stream = MVar.take readVar
            let! (Item(value, newV)) = MVar.take stream
            do! MVar.put readVar newV
            return value
        }
