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

namespace TradeEMACross.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SARController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private const string key = "ACTIVE_SAR_OBJECTS";
        private const string key2 = "ACTIVE_SAR2_OBJECTS";
        IConfiguration configuration;
        ZMQClient zmqClient;
        private IMemoryCache _cache;
        public SARController(IMemoryCache cache, IHttpClientFactory httpClientFactory)
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
        public async Task<ActiveAlgosView> Trade([FromBody] SARInputs sarInputs, int algoInstance = 0)
        {
            if (!sarInputs.FO)
            {
                StopAndReverseOptions sarOptions = 
                    new StopAndReverseOptions(sarInputs.BToken, sarInputs.Expiry, sarInputs.Qty, sarInputs.tpr, 
                    sarInputs.sl, algoInstance: 0, httpClientFactory: _httpClientFactory);

                sarOptions.OnOptionUniverseChange += ExpiryTrade_OnOptionUniverseChange;
                sarOptions.OnTradeEntry += OptionSellwithRSI_OnTradeEntry;
                sarOptions.OnTradeExit += OptionSellwithRSI_OnTradeExit;

                List<StopAndReverseOptions> activeAlgoObjects = _cache.Get<List<StopAndReverseOptions>>(key2);

                if (activeAlgoObjects == null)
                {
                    activeAlgoObjects = new List<StopAndReverseOptions>();
                }
                activeAlgoObjects.Add(sarOptions);
                _cache.Set(key, activeAlgoObjects);

                Task task = Task.Run(() => NMQClientSubscription(sarOptions, sarInputs.BToken));

                //await task;
                return new ActiveAlgosView
                {
                    aid = Convert.ToInt32(AlgoIndex.SARScalping),
                    an = Convert.ToString((AlgoIndex)AlgoIndex.SARScalping),
                    ains = sarOptions.AlgoInstance,
                    algodate = DateTime.Now.ToString("yyyy-MM-dd"),
                    binstrument = sarInputs.BToken.ToString(),
                    expiry = DateTime.Now.ToString("yyyy-MM-dd"),
                    lotsize = sarInputs.Qty,
                    mins = 0
                };
            }
            else
            {
                StopAndReverse sarFutures =
                    new StopAndReverse(sarInputs.BToken, sarInputs.Expiry, sarInputs.Qty, sarInputs.tpr,
                    sarInputs.sl, algoInstance: 0, httpClientFactory: _httpClientFactory);

                sarFutures.OnOptionUniverseChange += ExpiryTrade_OnOptionUniverseChange;
                sarFutures.OnTradeEntry += OptionSellwithRSI_OnTradeEntry;
                sarFutures.OnTradeExit += OptionSellwithRSI_OnTradeExit;

                List<StopAndReverse> activeAlgoObjects = _cache.Get<List<StopAndReverse>>(key);

                if (activeAlgoObjects == null)
                {
                    activeAlgoObjects = new List<StopAndReverse>();
                }
                activeAlgoObjects.Add(sarFutures);
                _cache.Set(key, activeAlgoObjects);

                Task task = Task.Run(() => NMQClientSubscription(sarFutures, sarInputs.BToken));

                //await task;
                return new ActiveAlgosView
                {
                    aid = Convert.ToInt32(AlgoIndex.SARScalping),
                    an = Convert.ToString((AlgoIndex)AlgoIndex.SARScalping),
                    ains = sarFutures.AlgoInstance,
                    algodate = DateTime.Now.ToString("yyyy-MM-dd"),
                    binstrument = sarInputs.BToken.ToString(),
                    expiry = DateTime.Now.ToString("yyyy-MM-dd"),
                    lotsize = sarInputs.Qty,
                    mins = 0
                };
            }
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

        private async Task NMQClientSubscription(StopAndReverse sarObject, uint token)
        {
            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(new List<uint>() { token });

            await zmqClient.Subscribe(sarObject);
        }

        private void ExpiryTrade_OnOptionUniverseChange(StopAndReverse source)
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
        private async Task NMQClientSubscription(StopAndReverseOptions sarObject, uint token)
        {
            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(new List<uint>() { token });

            await zmqClient.Subscribe(sarObject);
        }

        private void ExpiryTrade_OnOptionUniverseChange(StopAndReverseOptions source)
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
            return Task.FromResult((int)AlgoIndex.SARScalping);
        }

        [HttpPut("{ain}")]
        public bool Put(int ain, [FromBody] int start)
        {
            List<StopAndReverseOptions> activeAlgoObjects;
            if (!_cache.TryGetValue(key, out activeAlgoObjects))
            {
                activeAlgoObjects = new List<StopAndReverseOptions>();
            }

            StopAndReverseOptions algoObject = activeAlgoObjects.FirstOrDefault(x => x.AlgoInstance == ain);
            if (algoObject != null)
            {
                algoObject.StopTrade(!Convert.ToBoolean(start));
            }
            _cache.Set(key, activeAlgoObjects);

            return true;
        }
    }
}
