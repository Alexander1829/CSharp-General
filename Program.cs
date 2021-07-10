using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Web;

namespace Beeline.MobileId.Aggregator.Jobs
{
	public class Program
	{
		public static void Main(string[] args)
		{
			CreateHostBuilder(args).Build().Run();
		}

		public static IHostBuilder CreateHostBuilder(string[] args) =>
			Host.CreateDefaultBuilder(args)
        //.UseWindowsService() //if application will host like windows service.
				.ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>())
				.ConfigureLogging((hostingContext, logging) =>
				{
					logging.ClearProviders();
					logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
				})
				.UseNLog(new NLogAspNetCoreOptions
				{
					CaptureMessageTemplates = true,
					CaptureMessageProperties = true
				});
	  }
  }
