using Algos.TLogics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using ZMQFacade;
using Algorithms.Algorithms;
using Algorithms.Utilities;
using GlobalLayer;
using Global.Web;
using GlobalCore;
using System.Linq;
using System.Data;
using System.Threading.Tasks;
using Algorithms.Utilities.Views;
using Algos.Utilities.Views;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using FirebaseAdmin.Messaging;
using System.Net.Http;
namespace TradeEMACross.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AlertController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private const string key = "ACTIVE_ALERT_OBJECTS";
        IConfiguration configuration;
        ZMQClient zmqClient;
        private IMemoryCache _cache;
        public AlertController(IMemoryCache cache, IHttpClientFactory httpClientFactory)
        {
            this._cache = cache;
            this._httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public IEnumerable<BInstumentView> Get()
        {
            DataLogic dl = new DataLogic();
            List<Instrument> bInstruments = dl.RetrieveBaseInstruments();

            return (from n in bInstruments select new BInstumentView { InstrumentToken = n.InstrumentToken, TradingSymbol = n.TradingSymbol.Trim(' ') }).ToList();
        }

        [HttpGet("{token}")]
        public IEnumerable<string> OptionExpiries(uint token)
        {
            DataLogic dl = new DataLogic();
            List<string> expiryList = dl.RetrieveOptionExpiries(token);
            return expiryList;
        }

        [HttpPost]
        public async Task<ActiveAlgosView> Trade([FromBody] AlertInput alertInput, int algoInstance = 0)
        {
            uint instrumentToken = alertInput.BToken;
            TimeSpan candleTimeSpan = TimeSpan.FromMinutes(alertInput.CTF);

            FirebaseApp.Create(new AppOptions()
            {
                Credential = GoogleCredential.FromFile("private_key.json")
            });

            var registrationToken = "f7eP7xagh7Y:APA91bHT-hAN1TqJJWvIbzx_hLz-hTBIIuaO7ZD9K-zjj1lSAmxS3JbpIH3IOKg_m7HEs0eWm4WXtBrvIf6byEn3Lslps1DTPu3Btbmim7Lvs1NtfiaSzOVUNvK_KZyG3zHkK7ZLSqhp";

            FirebaseMessaging firebaseMessaging = FirebaseMessaging.DefaultInstance;

            //var message = new Message()
            //{
            //    Data = new Dictionary<string, string>() { { "Nifty", "17600" }, },
            //    Topic = Constants.MARKET_ALERTS,
            //    Notification = new Notification() { Title = "Index Alerts", Body = "Started" }
            //};
            //firebaseMessaging.SendAsync(message);


            ///FOR ALL STOCKS FUTURE , PASS INSTRUMENTTOKEN AS ZERO. FOR CE/PE ON BNF/NF SEND THE INDEX TOKEN AS INSTRUMENTTOKEN
            GenerateAlert alertGenerator =
                new GenerateAlert(candleTimeSpan, instrumentToken, alertInput.CSP, 
                firebaseMessaging, algoInstance, positionSizing: false, maxLossPerTrade: 0);

            //alertGenerator.OnOptionUniverseChange += ExpiryTrade_OnOptionUniverseChange;
           // alertGenerator.OnTradeEntry += OptionSellwithRSI_OnTradeEntry;
            //alertGenerator.OnTradeExit += OptionSellwithRSI_OnTradeExit;

            List<GenerateAlert> activeAlgoObjects = _cache.Get<List<GenerateAlert>>(key);

            if (activeAlgoObjects == null)
            {
                activeAlgoObjects = new List<GenerateAlert>();
            }
            activeAlgoObjects.Add(alertGenerator);
            _cache.Set(key, activeAlgoObjects);

            Task task = Task.Run(() => NMQClientSubscription(alertGenerator, instrumentToken));

            //await task;
            return new ActiveAlgosView
            {
                aid = Convert.ToInt32(AlgoIndex.GenerateAlert),
                an = Convert.ToString((AlgoIndex)AlgoIndex.GenerateAlert),
                ains = alertGenerator.AlgoInstance,
                algodate = DateTime.Now.ToString("yyyy-MM-dd"),
                binstrument = instrumentToken.ToString(),
                expiry = DateTime.Now.ToString("yyyy-MM-dd"),
                lotsize = 0,
                mins = alertInput.CTF
            };
        }

        private void OptionSellwithRSI_OnTradeExit(Order st)
        {
            //publish trade details and count
            //Bind with trade token details, use that as an argument
            OrderCore.PublishOrder(st);
            Thread.Sleep(100);
        }

        private void OptionSellwithRSI_OnTradeEntry(Order st)
        {
            //publish trade details and count
            OrderCore.PublishOrder(st);
            Thread.Sleep(100);
        }

        private async Task NMQClientSubscription(GenerateAlert alertGenerator, uint token)
        {
            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(new List<uint>() { token });

            await zmqClient.Subscribe(alertGenerator);
        }

        private void ExpiryTrade_OnOptionUniverseChange(GenerateAlert source)
        {
            try
            {
                zmqClient.AddSubscriber(source.SubscriptionTokens);
            }
            catch (Exception ex)
            {
                throw ex;

            }
        }
        [HttpGet("healthy")]
        public Task<int> Health()
        {
            return Task.FromResult((int)AlgoIndex.GenerateAlert);
        }

        [HttpPut("{ain}")]
        public bool Put(int ain, [FromBody] int start)
        {
            List<GenerateAlert> activeAlgoObjects;
            if (!_cache.TryGetValue(key, out activeAlgoObjects))
            {
                activeAlgoObjects = new List<GenerateAlert>();
            }

            GenerateAlert algoObject = activeAlgoObjects.FirstOrDefault(x => x.AlgoInstance == ain);
            if (algoObject != null)
            {
                algoObject.StopTrade(!Convert.ToBoolean(start));
            }
            _cache.Set(key, activeAlgoObjects);

            return true;
        }
    }
}
