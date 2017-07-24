using Blazor.Host;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Serialization;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Net.WebSockets;

namespace ClientServerApp.Server
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddResponseCompression(options =>
            {
                options.Providers.Add<GzipCompressionProvider>();
                options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] {
                    "application/octet-stream"
                });
            });
                
            // Add framework services.
            services.AddMvc().AddJsonOptions(opts =>
            {
                opts.SerializerSettings.ContractResolver = new DefaultContractResolver();
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            app.UseWebSockets();
            app.Map("/debug", (config) =>
           {
               config.Use(async (context, next) => {
                   var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                   var receiveTask = Task.Run(async () =>
                   {
                       while(true)
                       {
                           var buffer = new byte[1024 * 4];
                           var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                           var content = Encoding.UTF8.GetString(buffer, 0, result.Count);
                           Console.WriteLine(content);
                       }
                   });

                   var sendTask = Task.Run(async () =>
                   {
                       while (true)
                       {
                           var input = Console.ReadLine();
                           var buffer = Encoding.UTF8.GetBytes(input);
                           
                           await webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                       }
                   });

                   receiveTask.Wait();
                   sendTask.Wait();
               });
            });


            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseResponseCompression();
            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });

            var clientDirectory = Path.Combine("..", "ClientServerApp.Client");
            if (!Directory.Exists(clientDirectory))
            {
                clientDirectory = Directory.GetCurrentDirectory();
            }

            // All other requests handled by serving the SPA
            app.UseBlazorUI(clientDirectory, opts =>
            {
                opts.EnableServerSidePrerendering = true;
                opts.ClientAssemblyName = "ClientServerApp.Client.dll";
            });
        }
    }
}
