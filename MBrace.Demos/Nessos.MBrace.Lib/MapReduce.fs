namespace Nessos.MBrace.Lib

    module MapReduce =

        open Nessos.MBrace.Client

        /// A classic ML list implemented using CloudRefs.
        type CloudList<'T> = Nil | Cons of 'T * ICloudRef<CloudList<'T>>

        /// A binary tree structure implemented with CloudRef for the parent-child connection.
        type CloudTree<'T> = Empty | Leaf of 'T | Branch of (ICloudRef<CloudTree<'T>> * ICloudRef<CloudTree<'T>>)

        type ContainerState<'Container, 'T> =
            | Blank
            | Single of 'T
            | Split of 'Container * 'Container
    
        /// A representation of the current execution context.
        /// The context can be cloud distribution, local distribution across cpu cores,
        /// or sequential execution.
        type ParallelismContext = 
            | CloudParallel 
            | LocalParallel 
            | LocalSequential

        [<Cloud>]
        /// Get the next number of tasks to be spawn.
        let nextJobsNumber () = 
            cloud { 
                let! workers = Cloud.GetWorkerCount()
                return ((log(float workers) / log 2.) |> int) + 1
            }

        [<Cloud>]
        let rec mapReduce'' (mapF : 'T -> ICloud<'R>) 
                            (reduceF : 'R -> 'R -> ICloud<'R>) (identity : unit -> ICloud<'R>) 
                            (container : 'Container)
                            (containerF : 'Container -> ContainerState<'Container, 'T>) 
                            (context : ParallelismContext)
                            (depth : int)
                            (execute : ICloud<'R> -> ICloud<'R> -> ICloud<'R * 'R>) : ICloud<'R> =
            cloud {
                let state = containerF container
                match state with
                | Blank -> return! identity ()
                | Single value -> return! mapF value
                | Split (leftContainer, rightContainer) -> 
                    let! (left, right) = 
                        execute    (mapReduce' mapF reduceF identity leftContainer containerF  context depth)
                                    (mapReduce' mapF reduceF identity rightContainer containerF context depth)
                    return! reduceF left right
            }

        and [<Cloud>] mapReduce' (mapF : 'T -> ICloud<'R>) 
                            (reduceF : 'R -> 'R -> ICloud<'R>) (identity : unit -> ICloud<'R>) 
                            (container : 'Container)
                            (containerF : 'Container -> ContainerState<'Container, 'T>) 
                            (context : ParallelismContext) (depth : int) : ICloud<'R> =
            cloud {
                match context, depth with
                | CloudParallel, 0 ->
                    let! depth' = local <| nextJobsNumber()
                    return! mapReduce'' mapF reduceF identity container containerF  LocalParallel depth' (fun c1 c2 -> local <| c1 <||> c2)
                | CloudParallel, _ ->
                    return! mapReduce'' mapF reduceF identity container containerF CloudParallel (depth - 1) (<||>)
                | LocalParallel, 0 ->
                    return! mapReduce'' mapF reduceF identity container containerF LocalSequential depth (<.>)
                | LocalParallel, _ ->
                    return! mapReduce'' mapF reduceF identity container containerF LocalParallel (depth - 1) (fun c1 c2 -> local <| c1 <||> c2)
                | LocalSequential, _ ->
                    return! mapReduce'' mapF reduceF identity container containerF LocalSequential depth (<.>)
            }

        /// An implementation of map-reduce when the input is an array.
        and [<Cloud>] mapReduceArray (mapF : 'T [] -> ICloud<'R>) 
                            (reduceF : 'R -> 'R -> ICloud<'R>) (identity : unit -> ICloud<'R>) 
                            (values : 'T []) (partition : int) : ICloud<'R> =
            cloud {
                let! depth = nextJobsNumber()
                return! mapReduce' mapF reduceF identity values (fun (values : 'T []) -> 
                                                                    match values.Length with
                                                                    | 0 -> Blank
                                                                    | n when n <= partition -> Single values
                                                                    | _ -> values |> Array.split |> Split) CloudParallel depth
            }
        
        /// An implementation of map-reduce when walking a binary tree.
        and [<Cloud>] mapReduceCloudTree (mapF : 'T -> ICloud<'R>) 
                                    (reduceF : 'R -> 'R -> ICloud<'R>) (identity : unit -> ICloud<'R>) 
                                    (values : ICloudRef<CloudTree<'T>>) : ICloud<'R> =
            cloud {
                let! depth = nextJobsNumber()
                return! mapReduce' mapF reduceF identity values (fun (clouRefTree : ICloudRef<CloudTree<'T>>) -> 
                                                                    match clouRefTree.Value with
                                                                    | Empty -> Blank
                                                                    | Leaf value -> Single value
                                                                    | Branch (left, right) -> Split (left, right)) CloudParallel depth
            }

        [<Cloud>]
        /// An implementation of the classic recursive map-reduce with
        /// splitting in half.
        let rec mapReduce   (mapF : 'T -> ICloud<'R>) 
                            (reduceF : 'R -> 'R -> ICloud<'R>) (identity : 'R) 
                            (values : 'T list) : ICloud<'R> =
            cloud {
                match values with
                | [] -> return identity
                | [value] -> return! mapF value
                | _ -> 
                    let (leftList, rightList) = List.split values
                    let! (left, right) = 
                        (mapReduce mapF reduceF identity leftList) <||> 
                                    (mapReduce mapF reduceF identity rightList)
                    return! reduceF left right
            }
