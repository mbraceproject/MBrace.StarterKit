# MBrace.Azure

1. Clone this repo.
2. You will need [Azure SDK](http://azure.microsoft.com/en-us/downloads/) for the WorkerRole project.
3. Insert your Service Bus and Azure Storage connection strings in the [service configuration](azure/MBraceAzureService/ServiceConfiguration.Cloud.cscfg).
4. Build the three projects.
5. Publish the MBraceAzureService project.
6. Insert the same connection strings in [client.fsx](azure/MBraceAzureClient/client.fsx#15) and connect to your runtime.