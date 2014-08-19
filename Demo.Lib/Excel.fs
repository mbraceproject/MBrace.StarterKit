namespace Demo.Lib

    module Excel =

        ////Written by Mathias Brandewinder (Twitter: @brandewinder Blog: http://www.clear-lines.com/blog/)

        open Microsoft.Office.Interop.Excel
        open System.Runtime.InteropServices

        type XlChartType = Microsoft.Office.Interop.Excel.XlChartType

        // Attach to the running instance of Excel, if any
        let Attach () = 
            try
                Marshal.GetActiveObject("Excel.Application") 
                :?> Microsoft.Office.Interop.Excel.Application
                |> Some
            with
            | _ -> 
                printfn "Could not find running instance of Excel"
                None

        // Find the Active workbook, if any
        let Active () =
            let xl = Attach ()
            match xl with
            | None -> None
            | Some(xl) ->
                try
                    xl.ActiveWorkbook |> Some   
                with
                | _ ->
                    printfn "Could not find active workbook"
                    None

        let private flatten (arr: string [,]) =
            let iMax = Array2D.length1 arr
            let jMax = Array2D.length2 arr
            [| for i in 1 .. iMax ->
                [| for j in 1 .. jMax -> arr.[i,j] |] |]

        // Grab Selected Range, if any
        let Selection () =
            let xl = Attach ()
            match xl with
            | None -> None
            | Some(xl) ->
                try
                    let selection = xl.Selection :?> Range
                    selection.Value2 :?> System.Object [,] 
                    |> Array2D.map (fun e -> e.ToString())
                    |> flatten |> Some             
                with
                | _ ->
                    printfn "Invalid active selection"
                    None

        // Create a new Chart in active workbook
        let NewChart () =
            let wb = Active ()
            match wb with
            | None ->
                printfn "No workbook"
                None 
            | Some(wb) ->
                try
                    let charts = wb.Charts
                    charts.Add () :?> Chart |> Some
                with
                | _ -> 
                    printfn "Failed to create chart"
                    None

        // Plots single-argument function(s) over an interval
        type Plot (f: float -> float, over: float * float) =
            let mutable functions = [ f ]
            let mutable over = over
            let mutable grain = 50
            let chart = NewChart ()
            let values () = 
                let min, max = over
                let step = (max - min) / (float)grain
                [| min .. step .. max |]
            let draw f =
                match chart with
                | None -> ignore ()
                | Some(chart) -> 
                    let seriesCollection = chart.SeriesCollection() :?> SeriesCollection
                    let series = seriesCollection.NewSeries()
                    let xValues = values ()
                    series.XValues <- xValues
                    series.Values <- xValues |> Array.map f
            let redraw () =
                match chart with
                | None -> ignore ()
                | Some(chart) ->
                    let seriesCollection = chart.SeriesCollection() :?> SeriesCollection            
                    for s in seriesCollection do s.Delete() |> ignore
                    functions |> List.iter (fun f -> draw f)

            do
                match chart with
                | None -> ignore ()
                | Some(chart) -> 
                    chart.ChartType <- XlChartType.xlXYScatter
                    let seriesCollection = chart.SeriesCollection() :?> SeriesCollection
                    draw f

            member this.Add(f: float -> float) =
                match chart with
                | None -> ignore ()
                | Some(chart) ->
                    functions <- f :: functions
                    draw f

            member this.Rescale(min, max) =
                over <- (min, max)
                redraw()

            member this.Zoom(zoom: int) =
                grain <- zoom
                redraw()        

        // Plots surface of 2-argument function
        type Surface (f: float -> float -> float, xOver: (float * float), yOver: (float * float)) =
            let mutable xOver, yOver = xOver, yOver
            let mutable grain = 20
            let chart = NewChart ()
            let values over = 
                let min, max = over
                let step = (max - min) / (float)grain
                [| min .. step .. max |]

            let redraw () =
                match chart with
                | None -> ignore ()
                | Some(chart) ->
                    let xl = chart.Application
                    xl.ScreenUpdating <- false
                    let seriesCollection = chart.SeriesCollection() :?> SeriesCollection            
                    for s in seriesCollection do s.Delete()  |> ignore
                    let xs, ys = values xOver, values yOver
                    for x in xs do
                        let series = seriesCollection.NewSeries()
                        series.Name <- (string)x
                        series.XValues <- ys
                        series.Values <- ys |> Array.map (f x)
                    chart.ChartType <- XlChartType.xlSurfaceWireframe
                    xl.ScreenUpdating <- true

            do
                match chart with
                | None -> ignore ()
                | Some(chart) -> redraw ()

            member this.Rescale((xmin, xmax), (ymin, ymax)) =
                xOver <- (xmin, xmax)
                yOver <- (ymin, ymax)
                redraw ()

            member this.Zoom(zoom: int) =
                grain <- zoom
                redraw ()              

        // Create XY scatterplot, colored by group
        let scatterplot<'a when 'a: equality> (data: (float * float * 'a ) seq) =
            let chart = NewChart ()
            match chart with
            | None -> ignore ()
            | Some(chart) -> 
                let xl = chart.Application
                xl.ScreenUpdating <- false
                let seriesCollection = chart.SeriesCollection() :?> SeriesCollection
                let groups = data |> Seq.map (fun (_, _, g) -> g) |> Seq.distinct
                for group in groups do
                    let xs, ys, _ = data |> Seq.filter (fun (_, _, g) -> g = group) |> Seq.toArray |> Array.unzip3
                    let series = seriesCollection.NewSeries()
                    series.Name <- group.ToString()
                    series.XValues <- xs
                    series.Values <- ys
                chart.ChartType <- XlChartType.xlXYScatter
                xl.ScreenUpdating <- true

        // Create XY scatterplot, colored by group, with labels
        let labeledplot<'a when 'a: equality> (data: (float * float * 'a * string ) seq) =
            let chart = NewChart ()
            match chart with
            | None -> ignore ()
            | Some(chart) -> 
                let xl = chart.Application
                xl.ScreenUpdating <- false
                let seriesCollection = chart.SeriesCollection() :?> SeriesCollection
                let groups = data |> Seq.map (fun (_, _, g, _) -> g) |> Seq.distinct
                for group in groups do
                    let filtered = data |> Seq.filter (fun (_, _, g, _) -> g = group) |> Seq.toArray
                    let xs = filtered |> Array.map (fun (x, _, _, _) -> x)
                    let ys = filtered |> Array.map (fun (_, y, _, _) -> y)
                    let ls = filtered |> Array.map (fun (_, _, _, l) -> l)
                    let series = seriesCollection.NewSeries()
                    series.Name <- group.ToString()
                    series.XValues <- xs
                    series.Values <- ys
                    series.HasDataLabels <- true            
                    for i in 1 .. filtered.Length do 
                        let point = series.Points(i) :?> Point
                        point.DataLabel.Text <- ls.[i-1]
                chart.ChartType <- XlChartType.xlXYScatter
                xl.ScreenUpdating <- true

        let newChart () : Chart = 
            NewChart().Value

        let clear (c:Chart) =
            let sc = c.SeriesCollection() :?> Microsoft.Office.Interop.Excel.SeriesCollection
            for i in [sc.Count .. -1 .. 1] do sc.Item(i).Delete() |> ignore

        let draw (chart : Chart) (chartType : XlChartType) (name : string) (data : seq<'x * 'y>)  =
            clear chart
            chart.ChartType <- chartType
            let seriesCollection = chart.SeriesCollection() :?> SeriesCollection
            let series = seriesCollection.NewSeries()
            let (xs, ys) = data |> Seq.toArray |> Array.unzip
            series.Name <- name
            series.Values <- ys 
            series.XValues <- xs 
            chart
