namespace Demo.Lib

    open FSharp.Charting

    module Chart =

        let bar title (data : #seq<string * int>) =
            let ch = Chart.Bar(data, Title = title)
            ch.ShowChart() |> ignore

        let column title (data : #seq<string * int>) =
            let ch = Chart.Column(data, Title = title)
            ch.ShowChart() |> ignore

        let pie title (data : #seq<string * int>) =
            let ch = Chart.Pie(data, Title = title)
            ch.ShowChart() |> ignore