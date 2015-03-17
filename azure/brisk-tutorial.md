# mbrace-on-brisk-starter
Contains a set of scripts and demos to get you up and running with MBrace on Brisk.

# Get started With Brisk

Steps I used to create an [MBrace](http://www.m-brace.net/) Cluster on [Azure](https://windowsazure.com) using [Elastacloud Brisk Engine](https://www.briskengine.com/#/dash) as of 20/02/2015. See [the announcement of the pre-release availability](http://blog.brisk.elastatools.com/2015/02/19/adding-support-for-mbrace-f-and-net-on-brisk/) of this service.

Assumes you have an Azure account with at least 4 cores spare (there is a 20 core limit on some free or trial Azure accounts).

1.	Create an account with [Brisk](https://www.briskengine.com/), including entering your Azure account connection token details. When you select "Download Azure settings" Brisk will automatically take you to the Azure page that downloads a settings file.  You then load those settings into Brisk to complete creating your account.

2.	Create a new storage account in the [Azure Console](https://manage.windowsazure.com) (any name) or use an existing storage account.  Keep an eye on which data center you created your storage account in.

  ![pic3](https://cloud.githubusercontent.com/assets/7204669/6285351/a8257724-b8f2-11e4-9955-ceb19c53b7b4.jpg)

3.	Create a new MBrace cluster in [Brisk console](https://www.briskengine.com/#/dash) (use any name). Specify the right data centre (the same one as the storage account) and the right storage account.  This will take 5-10 minutes.

  ![pic4](https://cloud.githubusercontent.com/assets/7204669/6285354/b0620876-b8f2-11e4-84c9-58e7acee52ab.jpg)

  ![pic4b](https://cloud.githubusercontent.com/assets/7204669/6285356/b53f71c6-b8f2-11e4-964a-c3b89d17cf3e.png)

  ![pic4c](https://cloud.githubusercontent.com/assets/7204669/6285357/b55bcf4c-b8f2-11e4-905c-b782ae7b9c6a.png)


4.	Fetch the connection string details for your cluster  from Brisk by clicking on your cluster details, and looking at the Connection Strings tab. These will be needed in the next step.

5. Download the contents of this repository as a ZIP of clone and open in Visual Studio 2013.  If you don't have Visual Studio 2013, see http://fsharp.org/use/windows to get it.

6. In Visual Studio 2013, reset F# Interactive, enter the connection strings into the ``credentials.fsx``  script:

    ```fsharp
    let myStorageConnectionString = "DefaultEndpointsProtocol=..."
    let myServiceBusConnectionString = "Endpoint=sb://brisk..."
    ```

7. Called runtime.GetHandle(config):
    ```fsharp
    let runtime = Runtime.GetHandle(config)
    ```
   giving
   ```
   Binding session to 'C:\Users\dsyme\Documents\MBraceOnBriskStarter\MBraceOnBriskStarter\src\Demos\../../lib/Microsoft.Data.Edm.dll'...
   Binding session to 'C:\Users\dsyme\Documents\MBraceOnBriskStarter\MBraceOnBriskStarter\src\Demos\../../lib/Microsoft.Data.Services.Client.dll'...
   Binding session to 'C:\Users\dsyme\Documents\MBraceOnBriskStarter\MBraceOnBriskStarter\src\Demos\../../lib/Microsoft.Data.OData.dll'...
   Binding session to 'C:\Users\dsyme\Documents\MBraceOnBriskStarter\MBraceOnBriskStarter\src\Demos\../../lib/Newtonsoft.Json.dll'...

   val runtime : Runtime
    ```

8. Call runtime.ShowWorkers():
    ```
    Workers                                                                                                        

    Id                     Hostname        % CPU / Cores  % Memory / Total(MB)  Network(ul/dl : kbps)  Tasks  Process Id  Initialization Time         Heartbeat                  
    --                     --------        -------------  --------------------  ---------------------  -----  ----------  -------------------         ---------                  
    MBraceWorkerRole_IN_0  RD0003FF5507E5    1.78 / 2       22.08 / 3583.00         38.26 / 19.33      0 / 2        3316  20/02/2015 10:40:13 +00:00  20/02/2015 10:59:10 +00:00 
    MBraceWorkerRole_IN_2  RD0003FF550024    2.11 / 2       22.10 / 3583.00         45.91 / 23.32      0 / 2        3204  20/02/2015 10:40:15 +00:00  20/02/2015 10:59:09 +00:00 
    MBraceWorkerRole_IN_1  RD0003FF552704    2.43 / 2       22.19 / 3583.00         38.02 / 21.79      0 / 2         268  20/02/2015 10:40:18 +00:00  20/02/2015 10:59:10 +00:00 
    ```
    Called runtime.ShowProcesses():
    ```
    Processes                                                                                                   

    Name  Process Id  State  Completed  Execution Time  Tasks  Result Type  Start Time  Completion Time 
    ----  ----------  -----  ---------  --------------  -----  -----------  ----------  --------------- 

    Tasks : Active / Faulted / Completed / Total
    ```

9.	Create a cloud computation:
    ```fsharp

    let work0 =
        cloud { return sprintf "run in the cloud on worker '%s' " Environment.MachineName }
        |> runtime.CreateProcess
    ```
   giving:
    ```
    20022015 11:00:30.715 +00:00 : Creating process 6c31512670884c9eaa5655dc53e5cde1 
    20022015 11:00:30.719 +00:00 : Uploading dependencies
    20022015 11:00:30.726 +00:00 : FSI-ASSEMBLY_4f4cf9fc-e870-4681-9b03-bea847eff8c0_1, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
    20022015 11:00:31.611 +00:00 : Creating DistributedCancellationToken
    20022015 11:00:31.846 +00:00 : Starting process 6c31512670884c9eaa5655dc53e5cde1
    20022015 11:00:33.547 +00:00 : Created process 6c31512670884c9eaa5655dc53e5cde1

    val work0 : Process<string>
    ```

10. Check status:
    ```
    Process                                                                                                                                                                            

    Name                        Process Id      State  Completed  Execution Time            Tasks          Result Type  Start Time                  Completion Time            
    ----                        ----------      -----  ---------  --------------            -----          -----------  ----------                  ---------------            
          6c31512670884c9eaa5655dc53e5cde1  Completed  True       00:00:00.8355016    0 /   0 /   1 /   1  string       20/02/2015 11:00:35 +00:00  02/20/2015 11:00:36 +00:00 

    Tasks : Active / Faulted / Completed / Total
    ```

11. Check result:
  ```fsharp
  work0.IsCompleted
  ```
  giving
  ```fsharp
  true
  ```
   and
    ```fsharp
    work0.AwaitResultAsync() |> Async.RunSynchronously

    val it : string = "run in the cloud on worker 'RD0003FF550024' "
    ```
Awesome. Now go through the tutorials in this starter pack.  Read things carefully and take it step by step, don't execute the entire scripts in F# Interactive but rather take the time to execute each piece of code and understand what it's doing.

