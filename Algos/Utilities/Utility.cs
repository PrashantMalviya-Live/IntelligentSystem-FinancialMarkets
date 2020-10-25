using DataAccess;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GlobalLayer;
using ZConnectWrapper;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Algorithms.Utilities
{
    public class Utility
    {
        public static int GenerateAlgoInstance(AlgoIndex algoIndex, uint bToken, DateTime timeStamp, DateTime expiry,
            int initialQtyInLotsSize, int maxQtyInLotSize = 0, int stepQtyInLotSize = 0, decimal upperLimit = 0,
            decimal upperLimitPercent = 0, decimal lowerLimit = 0, decimal lowerLimitPercent = 0,
            float stopLossPoints = 0, int optionType = 0, float candleTimeFrameInMins = 5,
            CandleType candleType = CandleType.Time, int optionIndex = 0)
        {
            MarketDAO dao = new MarketDAO();
            return dao.GenerateAlgoInstance(algoIndex, bToken, timeStamp, expiry,
            initialQtyInLotsSize, maxQtyInLotSize, stepQtyInLotSize, upperLimit,
            upperLimitPercent, lowerLimit, lowerLimitPercent,
            stopLossPoints: stopLossPoints, optionType: optionType,
            candleTimeFrameInMins: candleTimeFrameInMins, candleType: candleType,
            optionIndex: optionIndex);
        }

        public static void LoadTokens()
        {
            List<Instrument> instruments = ZObjects.kite.GetInstruments(Exchange: "NFO");
            MarketDAO dao = new MarketDAO();
            dao.StoreInstrumentList(instruments);

            instruments = ZObjects.kite.GetInstruments(Exchange: "NSE");
            dao = new MarketDAO();
            dao.StoreInstrumentList(instruments);
        }
    }

    public static class KestrelServerOptionsExtensions
    {
        public static void ConfigureEndpoints(this KestrelServerOptions options)
        {
            var configuration = options.ApplicationServices.GetRequiredService<IConfiguration>();
            var environment = options.ApplicationServices.GetRequiredService<IHostingEnvironment>();

            var endpoints = configuration.GetSection("HttpServer:Endpoints")
                .GetChildren()
                .ToDictionary(section => section.Key, section =>
                {
                    var endpoint = new EndpointConfiguration();
                    section.Bind(endpoint);
                    return endpoint;
                });

            foreach (var endpoint in endpoints)
            {
                var config = endpoint.Value;
                var port = config.Port ?? (config.Scheme == "https" ? 443 : 80);

                var ipAddresses = new List<IPAddress>();
                if (config.Host == "localhost")
                {
                    ipAddresses.Add(IPAddress.IPv6Loopback);
                    ipAddresses.Add(IPAddress.Loopback);
                }
                else if (IPAddress.TryParse(config.Host, out var address))
                {
                    ipAddresses.Add(address);
                }
                else
                {
                    ipAddresses.Add(IPAddress.IPv6Any);
                }

                foreach (var address in ipAddresses)
                {
                    options.Listen(address, port,
                        listenOptions =>
                        {
                            if (config.Scheme == "https")
                            {
                                var certificate = LoadCertificate(config, environment);
                                listenOptions.UseHttps(certificate);
                                listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1;
                            }
                        });
                }
            }
        }

        private static X509Certificate2 LoadCertificate(EndpointConfiguration config, IHostingEnvironment environment)
        {
            if (config.StoreName != null && config.StoreLocation != null)
            {
                using (var store = new X509Store(config.StoreName, Enum.Parse<StoreLocation>(config.StoreLocation)))
                {
                    store.Open(OpenFlags.ReadOnly);
                    var certificate = store.Certificates.Find(
                        X509FindType.FindBySubjectName,
                        config.Host,
                        validOnly: !environment.IsDevelopment());

                    if (certificate.Count == 0)
                    {
                        throw new InvalidOperationException($"Certificate not found for {config.Host}.");
                    }

                    return certificate[0];
                }
            }

            if (config.FilePath != null && config.Password != null)
            {
                return new X509Certificate2(config.FilePath, config.Password);
            }

            throw new InvalidOperationException("No valid certificate configuration found for the current endpoint.");
        }
    }

    public class EndpointConfiguration
    {
        public string Host { get; set; }
        public int? Port { get; set; }
        public string Scheme { get; set; }
        public string StoreName { get; set; }
        public string StoreLocation { get; set; }
        public string FilePath { get; set; }
        public string Password { get; set; }
    }
}

