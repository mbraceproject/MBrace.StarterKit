using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using MBrace.Azure.Runtime;
using MBrace.Azure;

namespace MBraceAzureRole
{
    public class WorkerRole : RoleEntryPoint
    {
        private Service _svc;
        private Configuration _config;

        public override void Run()
        {
            _svc.Run();
        }

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections
            ServicePointManager.DefaultConnectionLimit = 512;

            /// Initialize global state for the current process
            Config.InitWorkerGlobalState();

            // Increase disk quota for mbrace filesystem cache.
            string customTempLocalResourcePath = RoleEnvironment.GetLocalResource("LocalMBraceCache").RootPath;
            Environment.SetEnvironmentVariable("TMP", customTempLocalResourcePath);
            Environment.SetEnvironmentVariable("TEMP", customTempLocalResourcePath);

            bool result = base.OnStart();

            _config = new Configuration(CloudConfigurationManager.GetSetting("MBrace.StorageConnectionString"), CloudConfigurationManager.GetSetting("MBrace.ServiceBusConnectionString"));

            _svc =
                RoleEnvironment.IsEmulated ?
                new Service(_config) : // Avoid long service names when using emulator
                new Service(_config, serviceId: RoleEnvironment.CurrentRoleInstance.Id.Split('.').Last());
            _svc.MaxConcurrentWorkItems = Environment.ProcessorCount * 8;

            RoleEnvironment.Changed += RoleEnvironment_Changed;

            return result;
        }

        void RoleEnvironment_Changed(object sender, RoleEnvironmentChangedEventArgs e)
        {
            foreach (var item in e.Changes.OfType<RoleEnvironmentTopologyChange>())
            {
                if (item.RoleName == RoleEnvironment.CurrentRoleInstance.Role.Name)
                {
                    // take any action needed on instance count modification; gracefully shrink etc
                }
            }

            foreach (var item in e.Changes.OfType<RoleEnvironmentConfigurationSettingChange>())
            {
                if (item.ConfigurationSettingName == "MBrace.ServiceBusConnectionString"
                    || item.ConfigurationSettingName == "MBrace.StorageConnectionString")
                {
                    _config = new Configuration(CloudConfigurationManager.GetSetting("MBrace.StorageConnectionString"), CloudConfigurationManager.GetSetting("MBrace.ServiceBusConnectionString"));
                    _svc.Stop();
                    _svc.Configuration = _config;
                    _svc.Start();
                }
            }
        }

        public override void OnStop()
        {
            base.OnStop();
            _svc.Stop();
        }
    }
}
