namespace Demo.Lib

    open Nessos.MBrace

    module Combinators =

        /// a map function that operates using partitioning;
        /// initial input is partitioned into chunks of fixed size,
        /// to be passed to worker nodes for execution using thread parallelism
        [<Cloud>]
        let chunkMap (f : 'T -> Cloud<'S>) partitionSize (inputs : 'T []) = cloud {
            let processLocal (inputs : 'T []) = cloud {
                return!
                    inputs
                    |> Array.map f
                    |> Cloud.Parallel
                    |> Cloud.ToLocal // force local/thread-parallel execution semantics
            }

            let! results =
                inputs
                |> Array.partition partitionSize
                |> Array.map processLocal
                |> Cloud.Parallel

            return Array.concat results
        
        }
            

        /// non-deterministic search combinator cloud workflows
        /// partitions an array into chunks thereby performing sequential
        /// thread-parallel search in every worker node.

        [<Cloud>]
        let tryFind (f : 'T -> bool) partitionSize (inputs : 'T []) =
            let searchLocal (inputs : 'T []) = cloud {
                return
                    inputs
                    |> Array.Parallel.map (fun t -> if f t then Some t else None)
                    |> Array.tryPick id
            }

            cloud {
                return!
                    inputs
                    |> Array.partition partitionSize
                    |> Array.map searchLocal
                    |> Cloud.Choice
            }