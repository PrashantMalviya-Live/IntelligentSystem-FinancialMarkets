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
using System.Net;

namespace RSIManagerService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DeltaStrangleController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private const string key = "ACTIVE_DELTA_STRANGLE_OBJECTS";
        IConfiguration configuration;
        ZMQClient zmqClient;
        private IMemoryCache _cache;
        public DeltaStrangleController(IMemoryCache cache, IHttpClientFactory httpClientFactory)
        {
            this._cache = cache;
            this._httpClientFactory = httpClientFactory;

            ServicePointManager.DefaultConnectionLimit = 10;
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
        public async Task<ActiveAlgosView> Trade([FromBody] StrangleWithDeltaandLevelInputs strangleWithDeltaandLevelInputs, int algoInstance = 0)
        {
            uint instrumentToken = strangleWithDeltaandLevelInputs.BToken;
            //DateTime endDateTime = DateTime.Now;
            //TimeSpan candleTimeSpan = TimeSpan.FromMinutes(optionBuyWithStraddleInput.CTF);

            DateTime expiry = strangleWithDeltaandLevelInputs.Expiry;
            //int optionQuantity = optionBuyWithStraddleInput.Qty;
            TimeSpan CTF = new TimeSpan(0, 5, 0);
            string userId = "PM27031981";//
#if local
            //endDateTime = Convert.ToDateTime("2020-10-16 12:21:00");
#endif
            ///FOR ALL STOCKS FUTURE , PASS INSTRUMENTTOKEN AS ZERO. FOR CE/PE ON BNF/NF SEND THE INDEX TOKEN AS INSTRUMENTTOKEN
            ManagedStrangleDelta straddleManager = new ManagedStrangleDelta(instrumentToken, expiry,
            strangleWithDeltaandLevelInputs.IQty, strangleWithDeltaandLevelInputs.StepQty, strangleWithDeltaandLevelInputs.MaxQty, strangleWithDeltaandLevelInputs.TP,
            strangleWithDeltaandLevelInputs.SL, strangleWithDeltaandLevelInputs.IDelta, strangleWithDeltaandLevelInputs.I2Delta, strangleWithDeltaandLevelInputs.MaxDelta, strangleWithDeltaandLevelInputs.MinDelta,
            strangleWithDeltaandLevelInputs.L1, strangleWithDeltaandLevelInputs.L1, strangleWithDeltaandLevelInputs.U1, strangleWithDeltaandLevelInputs.U2, AlgoIndex.DeltaStrangleWithLevels,
            CTF, _httpClientFactory, algoInstance, userid: userId);

            straddleManager.OnOptionUniverseChange += ExpiryTrade_OnOptionUniverseChange;
            straddleManager.OnTradeEntry += OptionSellwithRSI_OnTradeEntry;
            straddleManager.OnTradeExit += OptionSellwithRSI_OnTradeExit;

            List<ManagedStrangleDelta> activeAlgoObjects = _cache.Get<List<ManagedStrangleDelta>>(key);

            if (activeAlgoObjects == null)
            {
                activeAlgoObjects = new List<ManagedStrangleDelta>();
            }
            activeAlgoObjects.Add(straddleManager);
            _cache.Set(key, activeAlgoObjects);

            Task task = Task.Run(() => NMQClientSubscription(straddleManager, instrumentToken));


            

            //await task;
            return new ActiveAlgosView
            {
                aid = Convert.ToInt32(AlgoIndex.DeltaStrangle),
                an = Convert.ToString((AlgoIndex)AlgoIndex.DeltaStrangle),
                ains = straddleManager.AlgoInstance,
                algodate = DateTime.Now.ToString("yyyy-MM-dd"),
                binstrument = instrumentToken.ToString(),
                expiry = expiry.ToString("yyyy-MM-dd"),
                lotsize = strangleWithDeltaandLevelInputs.IQty,
                mins = 0 //strangleWithDeltaandLevelInputs.CTF
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

        private async Task NMQClientSubscription(ManagedStrangleDelta straddleManager, uint token)
        {
            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(new List<uint>() { token });

            await zmqClient.Subscribe(straddleManager);
        }

        private void ExpiryTrade_OnOptionUniverseChange(ManagedStrangleDelta source)
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
            return Task.FromResult((int)AlgoIndex.DeltaStrangle);
        }

        [HttpPut("{ain}")]
        public bool Put(int ain, [FromBody] int start)
        {
            List<ManagedStrangleDelta> activeAlgoObjects;
            if (!_cache.TryGetValue(key, out activeAlgoObjects))
            {
                activeAlgoObjects = new List<ManagedStrangleDelta>();
            }

            ManagedStrangleDelta algoObject = activeAlgoObjects.FirstOrDefault(x => x.AlgoInstance == ain);
            if (algoObject != null)
            {
                algoObject.StopTrade(!Convert.ToBoolean(start));
                //algoObject.PlaceOrder();
            }
            _cache.Set(key, activeAlgoObjects);

            return true;
        }
    }
}
