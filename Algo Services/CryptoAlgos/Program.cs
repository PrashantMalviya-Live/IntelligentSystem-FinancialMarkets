
using GrpcLoggerService;
using System.Diagnostics;
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
using CryptoAlgos.Controllers;
using System.Net.Http.Headers;
using BrokerConnectWrapper;

var isService = !(Debugger.IsAttached || args.Contains("--console"));

var pathToContentRoot = Directory.GetCurrentDirectory();
var webHostArgs = args.Where(arg => arg != "--console").ToArray();
if (isService)
{
    var pathToExe = Process.GetCurrentProcess().MainModule.FileName;
    pathToContentRoot = Path.GetDirectoryName(pathToExe);
}

var config = new ConfigurationBuilder()
   .SetBasePath(Directory.GetCurrentDirectory())
   .AddJsonFile("hostsettings.json", optional: true)
   .AddCommandLine(args)
   .Build();

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls(GlobalLayer.APIPORT.CryptoAlgos);

var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<TimeoutRejectedException>() // thrown by Polly's TimeoutPolicy if the inner execution times out
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromMilliseconds(600));
var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(10);
// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddGrpc();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ILoggerService, LoggerService>();
builder.Services.AddSingleton<IOrderService, OrderService>();

builder.Services.AddHttpClient("DeltaExchangeOrder", httpClient =>
{
}).AddPolicyHandler(retryPolicy).AddPolicyHandler(timeoutPolicy).SetHandlerLifetime(TimeSpan.FromMinutes(5));


var app = builder.Build();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapGrpcService<LoggerService>().EnableGrpcWeb()
                                      .RequireCors("AllowAll");
    
});
app.MapControllers();

app.Run();
