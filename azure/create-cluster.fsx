#load "../packages/MBrace.Azure/MBrace.Azure.fsx"
#load "../packages/MBrace.Azure.Management/MBrace.Azure.Management.fsx"

open MBrace.Azure.Management
    
let pubSettingsFile = @"... path to your downloaded publication settings file ... "
    
// create a cluster - optional arguments include VM size, cluster size etc.
let config = DeploymentManager.BeginDeploy(pubSettingsFile, Regions.North_Europe, VMSizes.A3, vmCount = 4)  // adjust your region as necessary

//If using the starter kit, note your connection strings and enter them in ``HandsOnTutorial/AzureCluster.fsx``
//

// Edit these lines:
//    let myStorageConnectionString = "..."
//    let myServiceBusConnectionString = "..."
//
// Inserting the values returned by:
//    config.StorageConnectionString
//     config.ServiceBusConnectionString

// Report on the status of provisioning by calling GetClusters
DeploymentManager.ShowDeployments pubSettingsFile

// Delete a cluster by supplying the cluster name
DeploymentManager.DeleteDeployment(pubSettingsFile, " ... cluster name ... ")