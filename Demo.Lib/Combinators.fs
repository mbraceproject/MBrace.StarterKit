namespace Demo.Lib

    open Nessos.MBrace

    module Combinators =

        /// non-deterministic search combinator cloud workflows
        /// partitions an array into chunks thereby performing sequential
        /// thread-parallel search in every worker node.

        [<Cloud>]
        let tryFind (f : 'T -> bool) partitionSize (inputs : 'T []) =
            let searchLocal (inputs : 'T []) =
                inputs
                |> Array.map (fun t -> cloud { return if f t then Some t else None })
                |> Cloud.Choice
                |> local // local combinator restricts computation to local async semantics

            cloud {
                return!
                    inputs
                    |> Array.partition partitionSize
                    |> Array.map searchLocal
                    |> Cloud.Choice
            }

