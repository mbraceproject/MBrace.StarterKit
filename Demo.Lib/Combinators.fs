namespace Demo.Lib

    open Nessos.MBrace

    module Combinators =

        /// <summary>
        ///     Sequential fold combinator.
        /// </summary>
        /// <param name="foldF">Folding function.</param>
        /// <param name="init">Initializer state.</param>
        /// <param name="inputs">Input array.</param>
        [<Cloud>]
        let seqFold (foldF : 'R -> 'T -> Cloud<'R>) (init : 'R) (inputs : 'T []) =
            let rec aux i state = cloud {
                if i = inputs.Length then
                    return state
                else
                    let! state' = foldF state inputs.[i]
                    return! aux (i+1) state
            }

            aux 0 init

        type private ParallelismContext =
            | Distributed
            | ThreadParallel
            | Sequential

        /// <summary>
        ///     Distributed fold combinator for cloud workflows.
        /// </summary>
        /// <param name="foldF">Fold function.</param>
        /// <param name="reduceF">Reduce function.</param>
        /// <param name="init">Get initial state.</param>
        /// <param name="inputs">input data.</param>
        [<Cloud>]
        let parFold (foldF : 'R -> 'T -> Cloud<'R>) 
                    (reduceF : 'R -> 'R -> Cloud<'R>) 
                    (init : unit -> 'R) 
                    (inputs : 'T []) =
    
            let rec aux ctx (inputs : 'T []) = cloud {
                if Array.isEmpty inputs then return init () else
                match ctx with
                | Distributed ->
                    let! size = Cloud.GetWorkerCount()
                    let chunks = Array.splitByPartitionCount size inputs
                    let! results = chunks |> Array.map (aux ThreadParallel) 
                                          |> Cloud.Parallel

                    return! seqFold reduceF (init()) results

                | ThreadParallel ->
                    let cores = System.Environment.ProcessorCount
                    let chunks = Array.splitByPartitionCount cores inputs
                    let! results = chunks |> Array.map (aux Sequential) 
                                          |> Cloud.Parallel 
                                          |> Cloud.ToLocal

                    return! seqFold reduceF (init()) results

                | Sequential -> return! seqFold foldF (init ()) inputs
            }

            aux Distributed inputs

        /// <summary>
        ///     Distributed Map/Reduce workflow combinator.
        /// </summary>
        /// <param name="mapper">Mapper workflow.</param>
        /// <param name="reducer">Reducer workflow</param>
        /// <param name="id">Identity element factory.</param>
        /// <param name="inputs">Initial inputs.</param>
        [<Cloud>]
        let mapReduce (mapper : 'T -> Cloud<'S>) (reducer : 'S -> 'S -> Cloud<'S>) 
                        (id : unit -> 'S) (inputs : 'T []) =

            parFold (fun s t -> cloud { let! s' = mapper t in return! reducer s s' })
                    reducer id inputs

        /// <summary>
        ///     a map function that operates using partitioning;
        ///     initial input is partitioned into chunks of fixed size,
        ///     to be passed to worker nodes for execution using thread parallelism
        /// </summary>
        /// <param name="f">map function.</param>
        /// <param name="partitionSize">partition size for every work item.</param>
        /// <param name="inputs">input data.</param>
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
                |> Array.splitByChunkSize partitionSize
                |> Array.map processLocal
                |> Cloud.Parallel

            return Array.concat results
        
        }
            

        /// <summary>
        ///     non-deterministic search combinator cloud workflows
        ///     partitions an array into chunks thereby performing sequential
        ///     thread-parallel search in every worker node.    
        /// </summary>
        /// <param name="f">predicate.</param>
        /// <param name="partitionSize">partition size for every work item.</param>
        /// <param name="inputs">input data.</param>
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
                    |> Array.splitByPartitionCount partitionSize
                    |> Array.map searchLocal
                    |> Cloud.Choice
            }