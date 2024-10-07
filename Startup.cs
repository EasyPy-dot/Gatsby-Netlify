using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.WebSockets;
using Microsoft.Extensions.Hosting;
using System.Net.WebSockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using static Microsoft.AspNetCore.Http.WebSocketManager;
using WebsocketManager;

namespace NetCoreServer
{
    public class Startup
    {
        public const int HTTP_PORT = 8088;
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddWebsocketService();
        }

        public  void Configure(IApplicationBuilder app, IHostEnvironment env, IServiceProvider service)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseWebSockets()
               .MapWebsocketManager("/TestWS", service.GetService<SimpleStockPriceTickerService>()!);
        }
    }
}
