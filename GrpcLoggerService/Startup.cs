using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GrpcPeerLoggerService;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
namespace GrpcLoggerService
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddGrpc();

            //services.AddSingleton<ILoggerService, LoggerService>();

            services.AddCors(o => o.AddPolicy("AllowAll", builder =>
            {
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader()
                       .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding");
            }));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            //app.UseGrpcWeb(); // Must be added between UseRouting and UseEndpoints
            app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true }); // Must be added between UseRouting and UseEndpoints

            app.UseCors();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<LoggerService>().EnableGrpcWeb().RequireCors("AllowAll"); //;
                endpoints.MapGrpcService<PeerLoggerService>().EnableGrpcWeb().RequireCors("AllowAll"); //;

                endpoints.MapGrpcService<OrderService>().EnableGrpcWeb().RequireCors("AllowAll"); //;
                endpoints.MapGrpcService<PeerOrderService>().EnableGrpcWeb().RequireCors("AllowAll"); //;

                endpoints.MapGrpcService<AlertService>().EnableGrpcWeb().RequireCors("AllowAll"); //;
                endpoints.MapGrpcService<PeerAlertService>().EnableGrpcWeb().RequireCors("AllowAll"); //;

                endpoints.MapGrpcService<ChartService>().EnableGrpcWeb().RequireCors("AllowAll"); //;
                endpoints.MapGrpcService<PeerChartService>().EnableGrpcWeb().RequireCors("AllowAll"); //;


                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
                });
            });
        }
    }
}
