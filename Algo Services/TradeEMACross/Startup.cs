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
using Polly.Extensions.Http;
using GrpcLoggerService;
using Polly.Timeout;
using Polly;
using System.Net.Http;
using TradeEMACross.Controllers;
using System.Net.Http.Headers;
using BrokerConnectWrapper;
using DataAccess;
using DBAccess;

namespace TradeEMACross
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
            services.AddGrpc();
            //services.AddCors(options =>
            //{
            //    options.AddPolicy("CorsPolicy", builder => builder
            //    .WithOrigins("http://localhost:4200")
            //    .AllowAnyMethod()
            //    .AllowAnyHeader()
            //    .AllowCredentials());
            //});

            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<TimeoutRejectedException>() // thrown by Polly's TimeoutPolicy if the inner execution times out
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromMilliseconds(600));
            //.RetryAsync(3);

            var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(10);

            services.AddControllers();
            services.AddMemoryCache();

            services.AddSingleton<ILoggerService, LoggerService>();
            services.AddSingleton<IOrderService, OrderService>();

            services.AddHttpClient("KotakPostOrder", httpClient =>
            {
            }).AddPolicyHandler(retryPolicy).AddPolicyHandler(timeoutPolicy).SetHandlerLifetime(TimeSpan.FromMinutes(5));


            var environment = Configuration["Environment"] ?? "Development";
            var secretName = environment == "Production" ? "prod/db/credentials" : "dev/db/credentials";

            using var scope = services.BuildServiceProvider().CreateScope();
            var secretHelper = scope.ServiceProvider.GetRequiredService<AwsSecretHelper>();
            var dbSecret = secretHelper.GetDbSecretAsync(secretName).Result;

            var connectionString = $"Server={dbSecret.host};Database={dbSecret.database};User Id={dbSecret.username};Password={dbSecret.password};";

            // Register DAO with constructor parameter (connection string)
            services.AddScoped<IRDSDAO>(sp =>
            {
                return environment == "Production"
                    ? new AWSRDSDAO(connectionString)
                    : new SQlDAO(connectionString);
            });

            services.AddScoped<ITimeStreamDAO>(sp =>
            {
                return environment == "Production"
                    ? new AWSTimestreamdb()
                    : new SQlDAO(connectionString);
            });

            //services.AddHttpClient("KotakPostOrder", httpClient =>
            //{

            //    httpClient.BaseAddress = new Uri("https://tradeapi.kotaksecurities.com/apim/orders/1.0/order/mis");
            //    httpClient.DefaultRequestHeaders.Add("accept", "application/json");
            //    httpClient.DefaultRequestHeaders.Add("consumerKey", ZObjects.kotak.ConsumerKey);
            //    httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + ZObjects.kotak.KotakAccessToken);
            //    httpClient.DefaultRequestHeaders.Add("sessionToken", ZObjects.kotak.UserSessionToken);
            //    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            //}).AddPolicyHandler(retryPolicy)
            //  .AddPolicyHandler(timeoutPolicy)
            //  .SetHandlerLifetime(TimeSpan.FromMinutes(5));

            //services.AddHttpClient("KotakGetOrder", httpClient =>
            //{
            //    httpClient.BaseAddress = new Uri("https://tradeapi.kotaksecurities.com/apim/orders/");
            //    httpClient.DefaultRequestHeaders.Add("accept", "application/json");
            //    httpClient.DefaultRequestHeaders.Add("consumerKey", ZObjects.kotak.ConsumerKey);
            //    httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + ZObjects.kotak.KotakAccessToken);
            //    httpClient.DefaultRequestHeaders.Add("sessionToken", ZObjects.kotak.UserSessionToken);
            //    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            //}).AddPolicyHandler(retryPolicy)
            //.AddPolicyHandler(timeoutPolicy)
            //.SetHandlerLifetime(TimeSpan.FromMinutes(5));


        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();


            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<LoggerService>().EnableGrpcWeb()
                                                  .RequireCors("AllowAll");
                endpoints.MapControllers();
            });
        }
    }
}
