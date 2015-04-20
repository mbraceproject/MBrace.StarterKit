# Get Started With Brisk

Steps to create an [MBrace](http://m-brace.net/) Cluster on [Azure](https://windowsazure.com) using [Elastacloud Brisk Engine](https://www.briskengine.com/#/dash) as of 20/02/2015. See [the announcement of the pre-release availability](http://blog.brisk.elastatools.com/2015/02/19/adding-support-for-mbrace-f-and-net-on-brisk/) of this service.

Assumes you have an Azure account with at least 4 cores spare (there is a 20 core limit on some free or trial Azure accounts).

1. Create an account with [Brisk](https://www.briskengine.com/), including entering your Azure account 
   connection token details. When you select "Download Azure settings" Brisk will 
   automatically take you to the Azure page that downloads a settings file.  
   You then load those settings into Brisk to complete creating your account.

2. Create a new MBrace cluster in [Brisk console](https://www.briskengine.com/#/dash) (use any name). This will take 5-10 minutes. You can specify specific storage and service bus endpoints if you wish or simply have Brisk automatically generate ones for you.

   ![pic4](https://cloud.githubusercontent.com/assets/7204669/6285354/b0620876-b8f2-11e4-84c9-58e7acee52ab.jpg)

   ![pic4b](https://cloud.githubusercontent.com/assets/7204669/6285356/b53f71c6-b8f2-11e4-964a-c3b89d17cf3e.png)

   ![pic4c](https://cloud.githubusercontent.com/assets/7204669/6285357/b55bcf4c-b8f2-11e4-905c-b782ae7b9c6a.png)

3. Fetch the connection string details for your cluster  from Brisk 
   by clicking on your cluster details, and looking at the Connection Strings tab. 
   These will be needed in the next step.

4. Download the contents of this repository as a ZIP (or clone) and open in 
   Visual Studio 2013.  If you don't have Visual Studio 2013, see http://fsharp.org/use/windows to get it.

5. In Visual Studio 2013, reset F# Interactive, enter the connection strings into the ``credentials.fsx``  script:

        let myStorageConnectionString = "DefaultEndpointsProtocol=..."
        let myServiceBusConnectionString = "Endpoint=sb://brisk..."

Now go through the hands-on tutorials in the starter pack.  Read things carefully and 
take it step by step, don't execute the entire scripts in F# Interactive but 
rather take the time to execute each piece of code and understand what it's doing.

Some examples of using the hands-on tutorials are below:

* Call runtime.GetHandle(config):

        let runtime = Runtime.GetHandle(config)

  giving

        val runtime : Runtime

* Call runtime.ShowWorkers():

        Workers                                                                                                        
	       
        Id                     Hostname        % CPU / Cores  % Memory / Total(MB)  Network(ul/dl : kbps)  Tasks  Process Id  Initialization Time         Heartbeat                  
        --                     --------        -------------  --------------------  ---------------------  -----  ----------  -------------------         ---------                  
        MBraceWorkerRole_IN_0  RD0003FF5507E5    1.78 / 2       22.08 / 3583.00         38.26 / 19.33      0 / 2        3316  20/02/2015 10:40:13 +00:00  20/02/2015 10:59:10 +00:00 
        MBraceWorkerRole_IN_2  RD0003FF550024    2.11 / 2       22.10 / 3583.00         45.91 / 23.32      0 / 2        3204  20/02/2015 10:40:15 +00:00  20/02/2015 10:59:09 +00:00 
        MBraceWorkerRole_IN_1  RD0003FF552704    2.43 / 2       22.19 / 3583.00         38.02 / 21.79      0 / 2         268  20/02/2015 10:40:18 +00:00  20/02/2015 10:59:10 +00:00 

* Create a cloud computation:

        let job =
            cloud { return sprintf "run in the cloud on worker '%s' " Environment.MachineName }
            |> runtime.CreateProcess

  giving:

        20022015 11:00:30.715 +00:00 : Creating process 6c31512670884c9eaa5655dc53e5cde1 
        20022015 11:00:30.719 +00:00 : Uploading dependencies
        20022015 11:00:30.726 +00:00 : FSI-ASSEMBLY_4f4cf9fc-e870-4681-9b03-bea847eff8c0_1, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
        20022015 11:00:31.611 +00:00 : Creating DistributedCancellationToken
        20022015 11:00:31.846 +00:00 : Starting process 6c31512670884c9eaa5655dc53e5cde1
        20022015 11:00:33.547 +00:00 : Created process 6c31512670884c9eaa5655dc53e5cde1
	    
        val job : Process<string>

* Check status:

        Process                                                                                                                                                                            
   	    
        Name                        Process Id      State  Completed  Execution Time            Tasks          Result Type  Start Time                  Completion Time            
        ----                        ----------      -----  ---------  --------------            -----          -----------  ----------                  ---------------            
              6c31512670884c9eaa5655dc53e5cde1  Completed  True       00:00:00.8355016    0 /   0 /   1 /   1  string       20/02/2015 11:00:35 +00:00  02/20/2015 11:00:36 +00:00 
   	    
        Tasks : Active / Faulted / Completed / Total

* Check result:

        job.IsCompleted

  giving

        true

   and

        job.AwaitResultAsync() |> Async.RunSynchronously
	    
        val it : string = "run in the cloud on worker 'RD0003FF550024' "

