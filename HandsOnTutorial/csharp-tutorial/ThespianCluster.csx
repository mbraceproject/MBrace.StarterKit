#r "../../packages/FSharp.Core/lib/net40/FSharp.Core.dll"
#r "../../packages/System.Runtime.Loader/lib/DNXCore50/System.Runtime.Loader.dll"
#r "../../packages/MBrace.Thespian/tools/FsPickler.dll"
#r "../../packages/MBrace.Thespian/tools/Vagabond.dll"
#r "../../packages/MBrace.Thespian/tools/Argu.dll"
#r "../../packages/MBrace.Thespian/tools/Newtonsoft.Json.dll"
#r "../../packages/MBrace.Thespian/tools/MBrace.Core.dll"
#r "../../packages/MBrace.Thespian/tools/MBrace.Runtime.dll"
#r "../../packages/MBrace.Thespian/tools/MBrace.Thespian.dll"
#r "../../packages/Streams/lib/net45/Streams.dll"
#r "../../packages/MBrace.Flow/lib/net45/MBrace.Flow.dll"
#r "../../packages/MBrace.CSharp/lib/net45/MBrace.CSharp.dll"

// before running sample, don't forget to set binding redirects to FSharp.Core in InteractiveHost.exe

using System;
using System.Linq;
using MBrace.Core;
using MBrace.Core.CSharp;
using MBrace.Flow.CSharp;
using MBrace.Library;
using MBrace.Thespian;

public class Config
{
    private static ThespianCluster current;

    /// <summary>
    ///     Gets or sets the desired number of Thespian worker instances
    /// </summary>
    public static int WorkerCount { get; set; }

    static Config() {
        WorkerCount = 4;
        ThespianWorker.LocalExecutable = "../packages/MBrace.Thespian/tools/mbrace.thespian.worker.exe";
    }

    /// <summary>
    /// Gets or initializes a local Thespian cluster
    /// </summary>
    /// <returns></returns>
    public static ThespianCluster GetCluster()
    {
        if (current == null)
            current = ThespianCluster.InitOnCurrentMachine(WorkerCount);

        return current;
    }

    /// <summary>
    /// Clears the local Thespian cluster instance
    /// </summary>
    public static void DeleteCluster()
    {
        if (current != null)
        {
            current.KillAllWorkers();
            current = null;
        }
    }
}