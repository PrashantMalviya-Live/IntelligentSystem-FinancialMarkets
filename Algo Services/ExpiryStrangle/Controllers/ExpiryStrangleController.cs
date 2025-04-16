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

namespace ExpiryStrangle.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExpiryStrangleController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private const string key = "ACTIVE_EXPIRY_TRADE_OBJECTS";
        IConfiguration configuration;
        ZMQClient zmqClient;
        private IMemoryCache _cache;
        public ExpiryStrangleController(IMemoryCache cache, IHttpClientFactory httpClientFactory)
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
        // GET api/<HomeController>/5
        [HttpGet("{token}")]
        public IEnumerable<string> OptionExpiries(uint token)
        {
            DataLogic dl = new DataLogic();
            List<string> expiryList = dl.RetrieveOptionExpiries(token);
            return expiryList;
        }

        [HttpPost]
        public async Task<ActiveAlgosView> Trade([FromBody] OptionExpiryStrangleInput optionExpiryStrangleInput, int algoInstance = 0)
        {
            uint bInstrumentToken = optionExpiryStrangleInput.BToken;
            DateTime endDateTime = DateTime.Now;
            DateTime expiry = optionExpiryStrangleInput.Expiry;
            int initialQuantity = optionExpiryStrangleInput.IQty;
            int stepQuantity = optionExpiryStrangleInput.SQty;
            int maxQuantity = optionExpiryStrangleInput.MQty;
            int stoploss = optionExpiryStrangleInput.SL;
            int targetProfit = optionExpiryStrangleInput.TP;
            int minDistanceFromBase = optionExpiryStrangleInput.MDFBI;
            int initialDistanceFromBase = optionExpiryStrangleInput.IDFBI;
            int minPremiumToTrade = optionExpiryStrangleInput.MPTT;
            optionExpiryStrangleInput.UID = "PM27031981";
            //var httpClient = _httpClientFactory.CreateClient();

            //ZMQClient();
            ExpiryTrade expiryTrade = new ExpiryTrade(bInstrumentToken, expiry, initialQuantity, 
                stepQuantity, maxQuantity, stoploss, minDistanceFromBase, minPremiumToTrade, initialDistanceFromBase, targetProfit, 
                optionExpiryStrangleInput.UID, 100, _httpClientFactory);
           
            expiryTrade.OnOptionUniverseChange += ExpiryTrade_OnOptionUniverseChange;
            expiryTrade.OnTradeEntry += ExpiryTrade_OnTradeEntry;
            expiryTrade.OnTradeExit += ExpiryTrade_OnTradeExit;

            List<ExpiryTrade> activeAlgoObjects = _cache.Get<List<ExpiryTrade>>(key);

            if (activeAlgoObjects == null)
            {
                activeAlgoObjects = new List<ExpiryTrade>();
            }
            activeAlgoObjects.Add(expiryTrade);
            _cache.Set(key, activeAlgoObjects);

            Task task = Task.Run(() => NMQClientSubscription(expiryTrade, bInstrumentToken));

            //await task;
            return new ActiveAlgosView
            {
                aid = Convert.ToInt32(AlgoIndex.ExpiryTrade),
                an = Convert.ToString((AlgoIndex)AlgoIndex.ExpiryTrade),
                ains = expiryTrade.AlgoInstance,
                algodate = endDateTime.ToString("yyyy-MM-dd"),
                binstrument = bInstrumentToken.ToString(),
                expiry = expiry.ToString("yyyy-MM-dd"),
                lotsize = maxQuantity,
                mins = 0
            };
        }
        private async Task NMQClientSubscription(ExpiryTrade expiryTrade, uint token)
        {
            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(new List<uint>() { token });

            await zmqClient.Subscribe(expiryTrade);
        }
        private void ExpiryTrade_OnTradeExit(Order st)
        {
            //publish trade details and count
            //Bind with trade token details, use that as an argument
            OrderCore.PublishOrder(st);
            Thread.Sleep(100);
        }

        private void ExpiryTrade_OnTradeEntry(Order st)
        {
            //publish trade details and count
            OrderCore.PublishOrder(st);
            Thread.Sleep(100);
        }

        //[HttpGet]
        //public void Get()
        //{
        //    StartService();
        //}

        //[HttpGet("positions/all")]
        //public IEnumerable<AlgoPosition> GetCurrentPositions()
        //{
        //    DataLogic dl = new DataLogic();
        //    DataSet dsActiveStrangles = dl.RetrieveActiveStrangles(AlgoIndex.ExpiryTrade);

        //    return null;
         
        //}

        private void ExpiryTrade_OnOptionUniverseChange(ExpiryTrade source)
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
            return Task.FromResult((int)AlgoIndex.ExpiryTrade);
        }

        [HttpPut("{ain}")]
        public bool Put(int ain, [FromBody] int start)
        {
            List<ExpiryTrade> activeAlgoObjects;
            if (!_cache.TryGetValue(key, out activeAlgoObjects))
            {
                activeAlgoObjects = new List<ExpiryTrade>();
            }

            ExpiryTrade algoObject = activeAlgoObjects.FirstOrDefault(x => x.AlgoInstance == ain);
            if (algoObject != null)
            {
                algoObject.StopTrade(!Convert.ToBoolean(start));
            }
            _cache.Set(key, activeAlgoObjects);

            return true;
        }
        ////[HttpPost]
        //public async Task CreateOptionStrategy(OptionStrategy ostrategy)
        //{
        //    ///Depending on strategy, UI will call apppropriate API. This API will be called to place manual orders only.
        //    DataLogic dl = new DataLogic();
        //    ostrategy.Id = dl.CreateOptionStrategy(ostrategy);

        //    MarketOrders orders = new MarketOrders();
        //    orders.PlaceOrder(ostrategy.Id, ostrategy.Orders);

        //}
    }
}