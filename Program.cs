using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;

namespace NetCoreServer
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            IWebHost host = CreateWebHostBuilder(args).Build();
            await host.RunAsync();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
            .UseStartup<Startup>();
    }


}
