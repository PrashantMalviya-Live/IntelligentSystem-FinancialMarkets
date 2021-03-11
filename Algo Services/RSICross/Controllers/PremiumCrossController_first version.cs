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

namespace RSICross.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PremiumCrossController : ControllerBase
    {
        private const string key = "ACTIVE_PREMIUMCROSS_OBJECTS";
        ZMQClient zmqClient;
        private IMemoryCache _cache;
        public PremiumCrossController(IMemoryCache cache)
        {
            this._cache = cache;
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
        public async Task<ActiveAlgosView> Trade([FromBody] PremiumCrossInput premiumCrossInput, int algoInstance = 0)
        {
            uint instrumentToken = premiumCrossInput.BToken;
            DateTime endDateTime = DateTime.Now;
            DateTime expiry = premiumCrossInput.Expiry;
            int optionQuantity = premiumCrossInput.Qty;

#if local
            endDateTime = Convert.ToDateTime("2020-10-16 12:21:00");
#endif
            ///FOR ALL STOCKS FUTURE , PASS INSTRUMENTTOKEN AS ZERO. FOR CE/PE ON BNF/NF SEND THE INDEX TOKEN AS INSTRUMENTTOKEN
            OptionBuyOnPremiumCross straddleManager =
                new OptionBuyOnPremiumCross(endDateTime, instrumentToken, expiry,
                optionQuantity, premiumCrossInput.TP, algoInstance);

            straddleManager.OnOptionUniverseChange += ExpiryTrade_OnOptionUniverseChange;
            straddleManager.OnTradeEntry += PremiumCross_OnTradeEntry;
            straddleManager.OnTradeExit += PremiumCross_OnTradeExit;

            List<OptionBuyOnPremiumCross> activeAlgoObjects = _cache.Get<List<OptionBuyOnPremiumCross>>(key);

            if (activeAlgoObjects == null)
            {
                activeAlgoObjects = new List<OptionBuyOnPremiumCross>();
            }
            activeAlgoObjects.Add(straddleManager);
            _cache.Set(key, activeAlgoObjects);

            Task task = Task.Run(() => NMQClientSubscription(straddleManager, instrumentToken));

            //await task;
            return new ActiveAlgosView
            {
                aid = Convert.ToInt32(AlgoIndex.PremiumCross),
                an = Convert.ToString((AlgoIndex)AlgoIndex.PremiumCross),
                ains = straddleManager.AlgoInstance,
                algodate = endDateTime.ToString("yyyy-MM-dd"),
                binstrument = instrumentToken.ToString(),
                expiry = expiry.ToString("yyyy-MM-dd"),
                lotsize = optionQuantity,
                mins = premiumCrossInput.CTF
            };
        }

        private void PremiumCross_OnTradeExit(Order st)
        {
            //publish trade details and count
            //Bind with trade token details, use that as an argument
            OrderCore.PublishOrder(st);
            Thread.Sleep(100);
        }

        private void PremiumCross_OnTradeEntry(Order st)
        {
            //publish trade details and count
            OrderCore.PublishOrder(st);
            Thread.Sleep(100);
        }

        private async Task NMQClientSubscription(OptionBuyOnPremiumCross straddleManager, uint token)
        {
            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(new List<uint>() { token });

            await zmqClient.Subscribe(straddleManager);
        }

        private void ExpiryTrade_OnOptionUniverseChange(OptionBuyOnPremiumCross source)
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
            return Task.FromResult((int)AlgoIndex.PremiumCross);
        }

        [HttpPut("{ain}")]
        public bool Put(int ain, [FromBody] int start)
        {
            List<OptionBuyOnPremiumCross> activeAlgoObjects;
            if (!_cache.TryGetValue(key, out activeAlgoObjects))
            {
                activeAlgoObjects = new List<OptionBuyOnPremiumCross>();
            }

            OptionBuyOnPremiumCross algoObject = activeAlgoObjects.FirstOrDefault(x => x.AlgoInstance == ain);
            if (algoObject != null)
            {
                algoObject.StopTrade(!Convert.ToBoolean(start));
            }
            _cache.Set(key, activeAlgoObjects);

            return true;
        }
    }
}
