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
using System.Net.Http;
using DataAccess;
using Newtonsoft.Json.Linq;

namespace RSIManagerService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StraddleController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private const string key = "ACTIVE_STRADDLE_OBJECTS_SHIFT";
        IConfiguration configuration;
        ZMQClient zmqClient;
        private IMemoryCache _cache;
        public StraddleController(IMemoryCache cache, IHttpClientFactory httpClientFactory)
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
        public async Task<ActiveAlgosView> Trade([FromBody] StraddleInput straddleInput, int algoInstance = 0)
        {
            //uint instrumentToken = straddleInput.BToken;
            //DateTime endDateTime = DateTime.Now;
            TimeSpan candleTimeSpan = TimeSpan.FromMinutes(5);

            //DateTime expiry = straddleInput.Expiry;
            //int optionQuantity = straddleInput.Qty;

            ///FOR ALL STOCKS FUTURE , PASS INSTRUMENTTOKEN AS ZERO. FOR CE/PE ON BNF/NF SEND THE INDEX TOKEN AS INSTRUMENTTOKEN
            //StraddleWithEachSideSL straddleManager =
            //    new StraddleWithEachSideSL(endDateTime, candleTimeSpan, instrumentToken, expiry,
            //    optionQuantity, 0, 1000000, straddleInput.SL, straddleInput.SS, straddleInput.TR, 
            //    positionSizing: false, maxLossPerTrade: 0);
            //var httpClient = _httpClientFactory.CreateClient();
            //StraddleWithEachSideSL straddleManager =
            //    new StraddleWithEachSideSL(straddleInput.BToken, straddleInput.Qty, straddleInput.uid, httpClientFactory: _httpClientFactory);

            //MultiStraddleDirectionalShift straddleManager =
            //    new MultiStraddleDirectionalShift(endDateTime, candleTimeSpan, instrumentToken, expiry,
            //    optionQuantity, 0, 1000000, straddleInput.SL, straddleInput.SS, straddleInput.TR,
            //    positionSizing: false, maxLossPerTrade: 0, httpClientFactory: _httpClientFactory);

            DataLogic dl = new DataLogic();
            straddleInput.Expiry = dl.GetCurrentWeeklyExpiry(DateTime.Today, straddleInput.BToken); //Convert.ToDateTime("2023-08-17");
            straddleInput.UID = "PM27031981";
            StraddleWithEachSideSL straddleManager =
                new StraddleWithEachSideSL(straddleInput.BToken, straddleInput.Qty, expiryDate: straddleInput.Expiry, uid: straddleInput.UID, httpClientFactory: _httpClientFactory);

            straddleManager.OnOptionUniverseChange += ExpiryTrade_OnOptionUniverseChange;
            straddleManager.OnTradeEntry += OptionSellwithRSI_OnTradeEntry;
            straddleManager.OnTradeExit += OptionSellwithRSI_OnTradeExit;

            List<StraddleWithEachSideSL> activeAlgoObjects = _cache.Get<List<StraddleWithEachSideSL>>(key);

            if (activeAlgoObjects == null)
            {
                activeAlgoObjects = new List<StraddleWithEachSideSL>();
            }
            activeAlgoObjects.Add(straddleManager);
            _cache.Set(key, activeAlgoObjects);

            Task task = Task.Run(() => NMQClientSubscription(straddleManager, straddleInput.BToken));

            //await task;
            return new ActiveAlgosView
            {
                aid = Convert.ToInt32(AlgoIndex.StraddleWithEachLegCutOff),
                an = Convert.ToString((AlgoIndex)AlgoIndex.StraddleWithEachLegCutOff),
                ains = straddleManager.AlgoInstance,
                algodate = DateTime.Now.ToString("yyyy-MM-dd"),
                binstrument = straddleInput.BToken.ToString(),
                expiry = DateTime.Now.ToString("yyyy-MM-dd"),
                lotsize = straddleInput.Qty,
                mins = 0
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

        private async Task NMQClientSubscription(StraddleWithEachSideSL straddleManager, uint token)
        {
            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(new List<uint>() { token });

            await zmqClient.Subscribe(straddleManager);
        }

        private void ExpiryTrade_OnOptionUniverseChange(StraddleWithEachSideSL source)
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
            return Task.FromResult((int)AlgoIndex.StraddleWithEachLegCutOff);
        }

        [HttpPut("{ain}")]
        public bool Put(int ain, [FromBody] int start)
        {
            List<StraddleWithEachSideSL> activeAlgoObjects;
            if (!_cache.TryGetValue(key, out activeAlgoObjects))
            {
                activeAlgoObjects = new List<StraddleWithEachSideSL>();
            }

            StraddleWithEachSideSL algoObject = activeAlgoObjects.FirstOrDefault(x => x.AlgoInstance == ain);
            if (algoObject != null)
            {
                algoObject.StopTrade(!Convert.ToBoolean(start));
            }
            _cache.Set(key, activeAlgoObjects);

            return true;
        }
    }
}
