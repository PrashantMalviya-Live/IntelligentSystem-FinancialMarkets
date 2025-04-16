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

namespace RSIManagerService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StraddleIndexRangeController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private const string key = "ACTIVE_STRADDLE_INDEX_RANGE_OBJECTS";
        IConfiguration configuration;
        ZMQClient zmqClient;
        private IMemoryCache _cache;
        public StraddleIndexRangeController(IMemoryCache cache, IHttpClientFactory httpClientFactory)
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
            TimeSpan candleTimeSpan = TimeSpan.FromMinutes(5);

            StraddleOnIndexRange straddleManager = new StraddleOnIndexRange(candleTimeSpan, straddleInput.BToken, straddleInput.Expiry,
                straddleInput.Qty, algoInstance: 0, straddleInput.UID, httpClientFactory: _httpClientFactory);

            straddleManager.OnOptionUniverseChange += ExpiryTrade_OnOptionUniverseChange;
            straddleManager.OnTradeEntry += OptionSellwithRSI_OnTradeEntry;
            straddleManager.OnTradeExit += OptionSellwithRSI_OnTradeExit;

            List<StraddleOnIndexRange> activeAlgoObjects = _cache.Get<List<StraddleOnIndexRange>>(key);

            if (activeAlgoObjects == null)
            {
                activeAlgoObjects = new List<StraddleOnIndexRange>();
            }
            activeAlgoObjects.Add(straddleManager);
            _cache.Set(key, activeAlgoObjects);

            Task task = Task.Run(() => NMQClientSubscription(straddleManager, straddleInput.BToken));

            //await task;
            return new ActiveAlgosView
            {
                aid = Convert.ToInt32(AlgoIndex.StraddleOnIndexRange),
                an = Convert.ToString((AlgoIndex)AlgoIndex.StraddleOnIndexRange),
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

        private async Task NMQClientSubscription(StraddleOnIndexRange straddleManager, uint token)
        {
            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(new List<uint>() { token });

            await zmqClient.Subscribe(straddleManager);
        }

        private void ExpiryTrade_OnOptionUniverseChange(StraddleOnIndexRange source)
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
            return Task.FromResult((int)AlgoIndex.StraddleOnIndexRange);
        }

        [HttpPut("{ain}")]
        public bool Put(int ain, [FromBody] int start)
        {
            List<StraddleOnIndexRange> activeAlgoObjects;
            if (!_cache.TryGetValue(key, out activeAlgoObjects))
            {
                activeAlgoObjects = new List<StraddleOnIndexRange>();
            }

            StraddleOnIndexRange algoObject = activeAlgoObjects.FirstOrDefault(x => x.AlgoInstance == ain);
            if (algoObject != null)
            {
                algoObject.StopTrade(!Convert.ToBoolean(start));
            }
            _cache.Set(key, activeAlgoObjects);

            return true;
        }
    }
}
