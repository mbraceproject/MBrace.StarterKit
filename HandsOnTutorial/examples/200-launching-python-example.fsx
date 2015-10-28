(*** hide ***)
#load "../ThespianCluster.fsx"
//#load "../AzureCluster.fsx"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit AzureCluster.fsx to enter your connection strings.


open System
open System.IO
open System.Net
open System.Diagnostics
open MBrace.Core
open MBrace.Flow
open Newtonsoft.Json

(**
# Example: Using custom provision of an MBrace cluster and running Python on the cluster

In this tutorial, you use the custom provision MBrace cluster to run a task that replies on Python code.

This tutorial preforms the following scenerio:
1. Use F# code to download a web page.
2. Pass the content of the web page to a Python script via standard input. The Python script extracts and returns all the hyperlinks from the web page.
3. Return all the extracted hyperlinks.

To run this tutorial, you first need to provision a MBrace cluster which contains the Python interepreter. You can use the
ProvisionWithPython solution in the StarterKit to do that.

*)

(** First connect to the cluster. *)
let cluster = Config.GetCluster() 

// You can connect to the cluster and get details of the workers in the pool:
cluster.ShowWorkers()


(** 
Next, create the Python code to extract hyperlinks from a web page.
The Python code uses the package beautifulsoup4, which will be installed by the custom provision of MBrace cluster. 
*)
let pythonCode = @"
from bs4 import BeautifulSoup
import sys
import json

# The web page data is read from standard input.
lines = [line for line in sys.stdin]
text = '\n'.join(lines)
html = BeautifulSoup(text)
links = html.select('a')
hrefs = [link['href'] for link in links]

# Output extracted links as a JSON list.
print(json.dumps(hrefs))
"

(** Then, define the Mbrace task to download web page and launch the Python interpreter using the Process class. *)
let job (url: string, pythonCode: string) = 
    cloud {
        // Download the web page.
        let content = (new WebClient()).DownloadString(url)
        
        // Create a local temp file on the client for the Python code.
        let pythonFile = Path.GetTempFileName()
        File.WriteAllText(pythonFile, pythonCode)

        // Launch the Python interpreter to extract hyperlinks.
        let prcInfo = ProcessStartInfo("c:\\Python27\\python.exe", pythonFile, UseShellExecute=false, 
                                       RedirectStandardInput=true, RedirectStandardOutput=true)

        let prc = new Process(StartInfo=prcInfo)
        prc.Start() |> ignore
        prc.StandardInput.Write(content)
        prc.StandardInput.Close()
        let output = prc.StandardOutput.ReadToEnd()
        prc.WaitForExit()
        let hrefs = JsonConvert.DeserializeObject<List<string>>(output);    
            
        // Delele the temp Python file.
        File.Delete(pythonFile)        
        return hrefs
    }

(** Finally, run the MBrace task and get the results. *)
let result = job("http://www.m-brace.net/", pythonCode) |> cluster.Run

(**
In this tutorial, you've learned how to launch an external process withing a cloud expression.
*)


