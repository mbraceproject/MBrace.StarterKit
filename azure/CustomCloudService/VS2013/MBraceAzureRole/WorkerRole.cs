using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.Azure;
using MBrace.Azure;
using MBrace.Azure.Runtime;

namespace MBraceAzureRole
{
    public class WorkerRole : RoleEntryPoint
    {
        private Service _svc;
        private Configuration _config;

        public override void Run()
        {
            try
            {
                _svc.Run();
            }
            catch (Exception ex)
            {
                Trace.TraceError("MBrace Azure Role unhandled exception: {0}", ex);
                throw;
            }
        }

        public override bool OnStart()
        {
            try
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
            catch (Exception ex)
            {
                Trace.TraceError("MBrace Azure Role unhandled exception: {0}", ex);
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
                        _config = new Configuration(CloudConfigurationManager.GetSetting("MBrace.StorageConnectionString"), CloudConfigurationManager.GetSetting("MBrace.ServiceBusConnectionString"));
                        _svc.Stop();
                        _svc.Configuration = _config;
                        _svc.Start();
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("MBrace Azure Role unhandled exception: {0}", ex);
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
                Trace.TraceError("MBrace Azure Role unhandled exception: {0}", ex);
                throw;
            }
        }
    }
}
