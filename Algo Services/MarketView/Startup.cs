using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using MarketView.Data;
using MarketView.Hubs;
using GrpcLoggerService;
using Microsoft.AspNetCore.Http;

namespace MarketView
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            //services.AddHttpsRedirection(opts =>
            //{
            //    opts.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
            //    opts.HttpsPort = 44300;
            //});

            //services.AddHttpsRedirection(opts =>
            //{
            //    opts.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
            //    opts.HttpsPort = 443;
            //});

            services.AddGrpc();

            services.AddControllers();
            //services.AddHttpsRedirection(options =>
            //{
            //    options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
            //    options.HttpsPort = 8087;
            //});

            services.AddDbContext<DBContext>(options =>
                    options.UseSqlServer(Configuration.GetConnectionString("DBContext")));

            //services.AddDbContext<DBContext>(options =>
            //        options.UseSqlServer(Configuration.GetConnectionString("DBContext")));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            //else
            //{
            //    app.UseHsts();
            //}

           // app.UseHttpsRedirection();

            app.UseRouting();

            //app.UseCors("CorsPolicy");

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                //endpoints.MapGrpcService<LoggerService>().EnableGrpcWeb()
                //                                  .RequireCors("AllowAll");
                //endpoints.MapHub<LogHub>("/log");
                //endpoints.MapHub<ChartHub>("/chart");
            });
        }
    }
}
