using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DataAccess;
using DBAccess;

namespace MarketDataService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
            .UseWindowsService()
            .UseSystemd()
            
                .ConfigureServices((hostContext, services) =>
                {
                    var environment = hostContext.Configuration["Environment"] ?? "Development";
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

                    services.AddHostedService<DSService>();
                });
        
    }
}
