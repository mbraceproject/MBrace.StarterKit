using System;
using System.Threading.Tasks;
using Microsoft.Owin;
using Owin;
using MBrace.AWS;
using MBrace.AWS.Service;

[assembly: OwinStartup(typeof(MBrace.AWS.WebWorker.Startup))]

namespace MBrace.AWS.WebWorker
{
    public class Startup
    {

        public void Configuration(IAppBuilder app)
        {
            var hostname = $"webWorker-{Environment.MachineName}";
            var creds = new MBraceAWSCredentials(Config.AccessKey, Config.SecretKey);
            var config = MBrace.AWS.Configuration.Define(Config.Region, creds);
            var service = new WorkerService(config, hostname);

            service.Start();
        }
    }
}
