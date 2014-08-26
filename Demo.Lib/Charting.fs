namespace Demo.Lib

    open System
    open System.Drawing
    open System.Windows.Forms.DataVisualization.Charting

    open FSharp.Charting

    module Chart =

        let bar title (data : #seq<string * int>) =
            let ch = Chart.Bar(data, Title = title)
            ch.ShowChart() |> ignore

        let pie title (data : #seq<string * int>) =
            let ch = Chart.Pie(data, Title = title)
            ch.ShowChart() |> ignore