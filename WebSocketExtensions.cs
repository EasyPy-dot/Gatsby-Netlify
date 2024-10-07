using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;


namespace WebsocketManager
{
    public static class WebsocketExtensions
    {
        public static IServiceCollection AddWebsocketService(this IServiceCollection services)
        {
            services.AddTransient<WebsocketConnection>();
            foreach (var type in Assembly.GetEntryAssembly()!.ExportedTypes)
            {
                if (type.GetTypeInfo().BaseType == typeof(WebsocketHandler))
                {
                    services.AddSingleton(type);
                }
            }
            return services;
        }

        public static IApplicationBuilder MapWebsocketManager(this IApplicationBuilder app, PathString path, WebsocketHandler handler)
        {
            return app.Map(path, (_app) => {
                _app.UseMiddleware<WebsocketMiddleware>(handler);
            });
        }
    }
}
