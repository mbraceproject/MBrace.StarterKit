using System;
using System.Linq;
using System.Diagnostics;
using Microsoft.Azure;
using Microsoft.WindowsAzure.ServiceRuntime;
using MBrace.Azure.Service;

namespace MBrace.Azure.CloudService.WorkerRole
{
    public class WorkerRole : RoleEntryPoint
    {
        private Configuration _config;
        private WorkerService _svc;

        public override void Run()
        {
            try
            {
                _svc.Run();
            }
            catch (Exception ex)
            {
                Trace.TraceError("MBrace.Azure.WorkerRole Run unhandled exception: {0}", ex);
                throw;
            }
        }

        public override bool OnStart()
        {
            try
            {
                // Increase disk quota for mbrace filesystem cache.
                string customTempLocalResourcePath = RoleEnvironment.GetLocalResource("LocalMBraceCache").RootPath;
                string storageConnectionString = CloudConfigurationManager.GetSetting("MBrace.StorageConnectionString");
                string serviceBusConnectionString = CloudConfigurationManager.GetSetting("MBrace.ServiceBusConnectionString");

                bool result = base.OnStart();

                _config = new Configuration(storageConnectionString, serviceBusConnectionString);
                _svc =
                    RoleEnvironment.IsEmulated ?
                    new WorkerService(_config, String.Format("computeEmulator-{0}", Guid.NewGuid().ToString("N").Substring(0, 30))) :
                    new WorkerService(_config, workerId: Environment.MachineName);

                _svc.WorkingDirectory = customTempLocalResourcePath;
                _svc.LogFile = "logs.txt";
                _svc.MaxConcurrentWorkItems = Environment.ProcessorCount * 8;

                RoleEnvironment.Changed += RoleEnvironment_Changed;

                return result;
            }
            catch (Exception ex)
            {
                Trace.TraceError("MBrace.Azure.WorkerRole OnStart unhandled exception: {0}", ex);
                throw;
            }
        }

        void RoleEnvironment_Changed(object sender, RoleEnvironmentChangedEventArgs e)
        {
            try
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
                        string storageConnectionString = CloudConfigurationManager.GetSetting("MBrace.StorageConnectionString");
                        string serviceBusConnectionString = CloudConfigurationManager.GetSetting("MBrace.ServiceBusConnectionString");
                        _config = new Configuration(storageConnectionString, serviceBusConnectionString);
                        _svc.Stop();
                        _svc.Configuration = _config;
                        _svc.Start();
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("MBrace.Azure.WorkerRole RoleEnvironment_Changed unhandled exception: {0}", ex);
                throw;
            }
        }

        public override void OnStop()
        {
            try
            {
                base.OnStop();
                _svc.Stop();
            }
            catch (Exception ex)
            {
                Trace.TraceError("MBrace.Azure.WorkerRole OnStop unhandled exception: {0}", ex);
                throw;
            }
        }
    }
}
