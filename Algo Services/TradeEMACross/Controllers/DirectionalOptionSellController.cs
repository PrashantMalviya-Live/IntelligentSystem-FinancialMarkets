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
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http;

namespace TradeEMACross.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DirectionalOptionSellController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private const string okey = "ACTIVE_DIRECTIONSELLONHISTORICALAVERAGE_OPTIONS";

        IConfiguration configuration;
        ZMQClient zmqClient;
        private IMemoryCache _cache;
        public DirectionalOptionSellController(IMemoryCache cache, IHttpClientFactory httpClientFactory)
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

        [HttpGet("activealgos")]
        public async Task<IEnumerable<ActiveAlgosView>> GetActiveAlgos()
        {
            return null;
        }


        [HttpPost]
        public async Task<ActiveAlgosView> Trade([FromBody] OptionIVSpreadInput optionIVSpreadInput, int algoInstance = 0)
        {
            uint instrumentToken = optionIVSpreadInput.BToken;
            DateTime endDateTime = DateTime.Now;

            DateTime expiry1 = optionIVSpreadInput.Expiry1;
            int optionQuantity = optionIVSpreadInput.StepQty;
            var httpClient = _httpClientFactory.CreateClient();

            optionIVSpreadInput.UID = "PM27031981";
#if local
            endDateTime = Convert.ToDateTime("2020-11-09 09:15:00");
#endif
            ///FOR ALL STOCKS FUTURE , PASS INSTRUMENTTOKEN AS ZERO. FOR CE/PE ON BNF/NF SEND THE INDEX TOKEN AS INSTRUMENTTOKEN
            DirectionSellOnHistoricalAvg directionSellOnHistoricalAvg =
                   new DirectionSellOnHistoricalAvg(endDateTime, instrumentToken, expiry1,
                   optionQuantity, optionIVSpreadInput.MaxQty, optionIVSpreadInput.StepQty, targetProfit: optionIVSpreadInput.TP, openSpread: optionIVSpreadInput.OpenVar,
                   closeSpread: optionIVSpreadInput.CloseVar, stopLoss: optionIVSpreadInput.SL, optionIVSpreadInput.UID,
                   algoInstance, false, 0, timeBandForExit: optionIVSpreadInput.TO, httpClient: httpClient);

                algoInstance = directionSellOnHistoricalAvg.AlgoInstance;

                directionSellOnHistoricalAvg.OnOptionUniverseChange += OptionBuywithRSI_OnOptionUniverseChange;
                directionSellOnHistoricalAvg.OnTradeEntry += OptionSellwithRSI_OnTradeEntry;
                directionSellOnHistoricalAvg.OnTradeExit += OptionSellwithRSI_OnTradeExit;

                List<DirectionSellOnHistoricalAvg> activeAlgoObjects = _cache.Get<List<DirectionSellOnHistoricalAvg>>(okey);

                if (activeAlgoObjects == null)
                {
                    activeAlgoObjects = new List<DirectionSellOnHistoricalAvg>();
                }
                activeAlgoObjects.Add(directionSellOnHistoricalAvg);
                _cache.Set(okey, activeAlgoObjects);


                Task task = Task.Run(() => NMQClientSubscription(directionSellOnHistoricalAvg, instrumentToken));
           
            //await task;
            return new ActiveAlgosView
            {
                aid = Convert.ToInt32(AlgoIndex.SellOnHistoricalAverage),
                an = Convert.ToString((AlgoIndex)AlgoIndex.SellOnHistoricalAverage),
                ains = algoInstance,
                algodate = endDateTime.ToString("yyyy-MM-dd"),
                binstrument = instrumentToken.ToString(),
                expiry = expiry1.ToString("yyyy-MM-dd"),
                lotsize = optionQuantity,
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

        private async Task NMQClientSubscription(CalendarSpreadValueScalping buyWithRSI, uint token)
        {
            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(new List<uint>() { token });

            await zmqClient.Subscribe(buyWithRSI);
        }
        private void OptionBuywithRSI_OnOptionUniverseChange(CalendarSpreadValueScalping source)
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
        private async Task NMQClientSubscription(DirectionSellOnHistoricalAvg buyWithRSI, uint token)
        {
            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(new List<uint>() { token });

            await zmqClient.Subscribe(buyWithRSI);
        }
        private void OptionBuywithRSI_OnOptionUniverseChange(DirectionSellOnHistoricalAvg source)
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
            return Task.FromResult((int)AlgoIndex.SellOnHistoricalAverage);
        }
        [HttpPut("{ain}")]
        public bool Put(int ain, [FromBody] int start)
        {
            List<DirectionSellOnHistoricalAvg> activeAlgoStraddleOptions;
            if (_cache.TryGetValue(okey, out activeAlgoStraddleOptions))
            {
                DirectionSellOnHistoricalAvg algoObject = activeAlgoStraddleOptions.FirstOrDefault(x => x.AlgoInstance == ain);
                if (algoObject != null)
                {
                    algoObject.StopTrade(!Convert.ToBoolean(start));
                }
                _cache.Set(okey, activeAlgoStraddleOptions);
            }
            return true;
        }

        //// GET api/<DirectionalOptionSellController>/5
        //[HttpGet("{id}")]
        //public string Get(int id)
        //{
        //    return "value";
        //}

        //// POST api/<DirectionalOptionSellController>
        //[HttpPost]
        //public void Post([FromBody] string value)
        //{
        //}

        //// PUT api/<DirectionalOptionSellController>/5
        //[HttpPut("{id}")]
        //public void Put(int id, [FromBody] string value)
        //{
        //}

        //// DELETE api/<DirectionalOptionSellController>/5
        //[HttpDelete("{id}")]
        //public void Delete(int id)
        //{
        //}
    }
}
