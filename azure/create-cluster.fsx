#load "../packages/MBrace.Azure/MBrace.Azure.fsx"

open MBrace.Azure
    
let pubSettingsFile = @"... path to your downloaded publication settings file ... "
    
// create a cluster - optional arguments include VM size, cluster size etc.
let config = Management.CreateCluster(pubSettingsFile, Regions.North_Europe)  // adjust your region as necessary

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
Management.GetClusters(pubSettingsFile)

// Delete a cluster by supplying the cluster name
Management.DeleteCluster(pubSettingsFile, " ... cluster name ... ")

