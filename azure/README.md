# MBrace on Azure

## Provisioning Your MBrace Cluster in Azure

An MBrace cluster is provisioned on Azure by deploying an Azure Cloud Service with MBrace Worker Roles.
The directory contains template solutions for this.

You will need the [Azure SDK 2.7](http://azure.microsoft.com/en-us/downloads/).

1. Make a copy of the `CustomCloudService` directory.

2. Create an Azure account using the [Azure management portal](https://manage.windowsazure.com/).

3. Using the Azure management portal:

   * Create a storage account. Use any name (e.g. `mbrace10storage`).  Note the name and the primary access key.

   * Create a service bus account. Use any name (e.g. `mbrace10bus`). Note the `RootManageSharedAccessKey` connection string in the connection information.

4. Insert these names and keys into the `ServiceConfiguration.Cloud.cscfg` file.

5. Right click and Publish the `MBrace.Azure.CloudService` project.  During publication, choose a new name for your cloud service. 


After your service is published it should appear as a cloud service in the [Azure management portal](https://manage.windowsazure.com/).

If using the hands-on tutorials, insert the same connection trings in [MBraceCluster.fsx](../HandsOnTutorial/AzureCluster.fsx#L24) and connect 
to your runtime (see [hello-world.fsx](../HandsOnTutorial/1-hello-world.fsx) for an example).

## Cluster Customization

During configuration (and prior to deployment) you may want to:

* adjust the size of VM used for worker instances in your cluster; or

* add endpoints to your cloud service (so your MBrace cluster can publish 
  TCP and HTTP endpoints, either public or to your virtual network, 
  for example, you want your MBrace cluster to publish a web server); or

* enable Remote Access to MBrace cluster worker instances; or

* specify the size of local storage available on MBrace cluster worker instances; or

* upload certificates as part of your provisioning process; or

* specify Azure-specific local caching options; or

* include additional web and worker roles in your cloud service; or

* compile and deploy your own version of the MBrace cluster worker instance software. 

In order to provision explicitly, as a prerequisite you need 
to have an Azure account, basic knowledge of the Azure computing and
an editing environment supporting F#.

### Creating custom MBrace Worker Roles with Python installed

Sometimes, you want to customize the MBrace cluster with extra software installed on all workers. 
For example, you have a machine learning algorithm written in Python and you want to use MBrace to launch it. So you  need to have the Python interpreter and all the dependant Python packages pre-installed in the MBrace cluster.

This section describes how to create a MBrace cluster with third-party software installation, and use it from the Mbrace client. The task at hand is:

The demo code to create a custom MBrace cluster is located at the `ProvisionWithPython` folder. It is adapted from the default custom provision solution at the `CustomCloudService` folder and has the following additional steps:

1. The MBrace cluster is implemented as an Azure worker role. A worker role can be configured to launch a startup script when it is initialized.  I follow the steps [here](http://blogs.msdn.com/b/cclayton/archive/2012/05/17/windows-azure-start-up-tasks-part-1.aspx) to enable the startup script:

1.1. Add a `startup.md` script in the `MBraceAzureRole` project. In the Properties panel of this file, set `Build Action` to `Content` and and `Copy to Output Directory` to `Always Copy`.
    
1.2. In the file `MBraceAzureService.ServiceDefinition.csdef`, add the following section after the `<ConfigurationSettings>` section:

    <Startup>
        <Task commandLine="startup.cmd" executionContext="elevated" taskType="simple"></Task>
    </Startup>

This XML section registers the startup.md file in the worker role initialization process.
    
1.3. Add the following line to `startup.md`

   PowerShell -ExecutionPolicy Unrestricted .\startup.ps1

This line launches a PowerShell script `startup.ps1` (to be added as well). This is needed because third-party software installation is much easier to do in PowerShell script but the format of startup.md is Windows batch. Some tips in using PowerShell in Azure worker role startup script are [here](https://msdn.microsoft.com/en-us/library/azure/jj130675.aspx).
    
1.4 Add a new script `startup.ps1` in the `MBraceAzureRole`, and set `Build Action` to `Content` and and `Copy to Output Directory` to `Always Copy`.
    
1.5 In the `startup.ps1` file, add the following PowerShell code to install Python and setup the beautifulsoup package:

	# Download Python installer
	echo "Downloading python."
	$url="https://www.python.org/ftp/python/2.7.9/python-2.7.9.msi"
	Invoke-WebRequest $url -OutFile c:\python-2.7.9.msi

	# Install Python
	echo "Installing python."
	msiexec /a c:\python-2.7.9.msi TARGETDIR=c:\Python27 ALLUSERS=1 ADDLOCAL=ALL /qn
	$newPath = $env:Path + ";c:\Python27;c:\Python27\Scripts"
	[Environment]::SetEnvironmentVariable("Path", $newPath, "Machine")

	# Sleep for a few seconds so the settings will have time to take effect.
	sleep 10

	# Export python path.
	$env:Path = $env:Path + ";c:\Python27;c:\Python27\Scripts"

	# Install pip
	echo "Installing pip."
	Invoke-WebRequest https://bootstrap.pypa.io/get-pip.py -OutFile c:\Python27\get-pip.py
	python c:\Python27\get-pip.py

	sleep 10
	echo "Installing beautifulsoup4."
	# Install beautifulsoup4 to parse web pages.
	pip install beautifulsoup4

	echo "Done."
    

1.6 Enable the permission to launch processes by adding the line with the Runtime tag in the worker role's csdef file, under the WorkerRole section:

    <WorkerRole name="MBrace.Azure.WorkerRole" vmsize="Large">
            <Runtime executionContext ="elevated" />
            ...
    
Then, you can follow the usual worker role publishing steps to publish the MBrace cluster. If you enable remote desktop on the worker roles, after they are published, you can login to those machines to verify that Python has been installed.

Now, you can now go to the 200-launching-python tutorial in the HandsOnTutorial, and see how to use the newly built custom provision.

### Customizing a cluster to install Java

	# Download JDK.
	# Instructions are taken from http://stackoverflow.com/questions/24430141/downloading-jdk-using-powershell.
	echo "Downloading JDK."
	$source = "http://download.oracle.com/otn-pub/java/jdk/8u60-b27/jdk-8u60-windows-x64.exe"
	$destination = "c:\jdk.exe"
	$client = new-object System.Net.WebClient 
	$cookie = "oraclelicense=accept-securebackup-cookie"
	$client.Headers.Add([System.Net.HttpRequestHeader]::Cookie, $cookie) 
	$client.downloadFile($source, $destination)

	# Install JDK.
	# Following the instruction here: http://docs.oracle.com/javase/7/docs/webnotes/install/windows/jdk-installation-windows.html
	c:\jdk.exe /s ADDLOCAL="ToolsFeature,SourceFeature,PublicjreFeature"
	# Sleep for 60 seconds, waiting for the JDK installation to finish.
	# This is needed because the non-interactive installation terminates immediately while the installation is working in background.
	sleep 60

	# Set JDK folder in PATH
	$newPath = $env:Path + ";" + $env:programfiles + "\Java\jdk1.8.0_60\bin"
	[Environment]::SetEnvironmentVariable("Path", $newPath, "Machine")

	echo "Done."
