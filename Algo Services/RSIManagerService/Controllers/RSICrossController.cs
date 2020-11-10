using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using GlobalLayer;
using Algos.TLogics;
using Algorithms.Utilities;
using ZMQFacade;
using System.Data;
using Microsoft.Extensions.Configuration;
using Algorithms.Algorithms;
using Algorithms.Utilities.Views;
using Global.Web;
using GlobalCore;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;
using Algos.Utilities.Views;

namespace RSIManagerService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RSICrossController : ControllerBase
    {
        private const string key = "ACTIVE_RSICROSS_OBJECTS";
        IConfiguration configuration;
        ZMQClient zmqClient;
        private IMemoryCache _cache;

        public RSICrossController(IMemoryCache cache)
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

        [HttpGet("activealgos")]
        public async Task<IEnumerable<ActiveAlgosView>> GetActiveAlgos()
        {
            DataLogic dl = new DataLogic();
            DataSet ds = dl.GetActiveAlgos(AlgoIndex.SellOnRSICross);

            List<ActiveAlgosView> activeAlgos = new List<ActiveAlgosView>();

            DataTable dtActiveAlgos = ds.Tables[0];
            DataRelation algo_orders_relation = ds.Relations.Add("Algo_Orders", new DataColumn[] { ds.Tables[0].Columns["Id"] },
                new DataColumn[] { ds.Tables[2].Columns["AlgoInstance"] });

            foreach (DataRow drAlgo in dtActiveAlgos.Rows)
            {
                ActiveAlgosView algosView = new ActiveAlgosView();

                OptionSellOnRSICrossInput algoInput = new OptionSellOnRSICrossInput
                {
                    Expiry = Convert.ToDateTime(drAlgo["Expiry"]),
                    CTF = Convert.ToInt32(drAlgo["CandleTimeFrame_Mins"]),
                    Qty = Convert.ToInt32(drAlgo["InitialQtyInLotSize"]),
                    BToken = Convert.ToUInt32(drAlgo["BToken"]),
                    MinDFBI = Convert.ToInt32(DBNull.Value != drAlgo["Arg5"] ? drAlgo["Arg5"] : 0),
                    MaxDFBI = Convert.ToInt32(DBNull.Value != drAlgo["Arg4"] ? drAlgo["Arg4"] : 0),
                    RMX = Convert.ToDecimal(DBNull.Value != drAlgo["Arg2"] ? drAlgo["Arg2"] : 0),
                };
                algosView.aid = Convert.ToInt32(drAlgo["AlgoId"]);
                algosView.an = Convert.ToString((AlgoIndex)algosView.aid);
                algosView.ains = Convert.ToInt32(drAlgo["Id"]);

                algosView.expiry = Convert.ToDateTime(drAlgo["Expiry"]).ToString("yyyy-MM-dd");
                algosView.mins = Convert.ToInt32(drAlgo["CandleTimeFrame_Mins"]);
                algosView.lotsize = Convert.ToInt32(drAlgo["InitialQtyInLotSize"]);
                algosView.binstrument = Convert.ToString(drAlgo["BToken"]);
                algosView.algodate = Convert.ToDateTime(drAlgo["Timestamp"]).ToString("yyyy-MM-dd");

                DataRow[] drOrders = drAlgo.GetChildRows(algo_orders_relation);
                List<Order> orders = new List<Order>();
                foreach (DataRow drOrder in drOrders)
                {
                    Order o = ViewUtility.GetOrder(drOrder);
                    orders.Add(o);
                }

                var lastExecutedOrders = orders.Where(o => o.Status.ToUpper() == Constants.ORDER_STATUS_COMPLETE
                && o.OrderType.ToUpper() != Constants.ORDER_TYPE_SLM
                && (o.Tag == null || o.Tag.ToUpper() != "NW")).OrderByDescending(x => x.OrderTimestamp);

                var lastOrder = lastExecutedOrders.FirstOrDefault();

                if (lastOrder != null && lastOrder.TransactionType.ToLower() == "sell")
                {
                    algosView.Orders.Add(ViewUtility.GetOrderView(lastOrder));
                    algoInput.ActiveOrder = lastOrder;

                    Trade(algoInput, algosView.ains);

                    activeAlgos.Add(algosView);
                }
            }
            return activeAlgos.ToArray();
        }


        [HttpPost]
        public async Task<ActiveAlgosView> Trade([FromBody] OptionSellOnRSICrossInput algoInput, int algoInstance = 0)
        {
            uint instrumentToken = algoInput.BToken;
            DateTime endDateTime = DateTime.Now;
            TimeSpan candleTimeSpan = TimeSpan.FromMinutes(algoInput.CTF);

            DateTime? expiry = algoInput.Expiry; // Convert.ToDateTime("2020-10-01");
            //decimal strikePriceRange = 1;
            int optionQuantity = algoInput.Qty;

#if local
            endDateTime = Convert.ToDateTime("2020-10-15 09:15:00");
#endif

            ///FOR ALL STOCKS FUTURE , PASS INSTRUMENTTOKEN AS ZERO. FOR CE/PE ON BNF/NF SEND THE INDEX TOKEN AS INSTRUMENTTOKEN
            OptionSellOnRSICross sellOnRSICross =
                new OptionSellOnRSICross(candleTimeSpan, instrumentToken, endDateTime, expiry,
                optionQuantity, algoInput.MaxDFBI, algoInput.MinDFBI, algoInput.RMX, algoInstance);

            sellOnRSICross.LoadActiveOrders(algoInput.ActiveOrder);
            sellOnRSICross.OnOptionUniverseChange += ExpiryTrade_OnOptionUniverseChange;
            sellOnRSICross.OnTradeEntry += SellOnRSICross_OnTradeEntry;
            sellOnRSICross.OnTradeExit += SellOnRSICross_OnTradeExit;

            List<OptionSellOnRSICross> activeAlgoObjects = null;
            activeAlgoObjects = _cache.TryGetValue(key, out activeAlgoObjects)? activeAlgoObjects : new List<OptionSellOnRSICross>();

            activeAlgoObjects.Add(sellOnRSICross);
            _cache.Set(key, activeAlgoObjects);


            Task task = Task.Run(() => NMQClientSubscription(sellOnRSICross, instrumentToken));

            //await task;
            return new ActiveAlgosView
            {
                aid = Convert.ToInt32(AlgoIndex.SellOnRSICross),
                an = Convert.ToString((AlgoIndex)AlgoIndex.SellOnRSICross),
                ains = sellOnRSICross.AlgoInstance,
                algodate = endDateTime.ToString("yyyy-MM-dd"),
                binstrument = instrumentToken.ToString(),
                expiry = expiry.HasValue ? expiry.Value.ToString("yyyy-MM-dd") : "",
                lotsize = optionQuantity,
                mins = algoInput.CTF
            };
        }

        private void SellOnRSICross_OnTradeExit(Order st)
        {
            OrderCore.PublishOrder(st);
            Thread.Sleep(100);
        }

        private void SellOnRSICross_OnTradeEntry(Order st)
        {
            OrderCore.PublishOrder(st);
            Thread.Sleep(100);
        }

        private async Task NMQClientSubscription(OptionSellOnRSICross sellOnRSICross, uint token)
        {
            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(new List<uint>() { token });

            await zmqClient.Subscribe(sellOnRSICross);
        }
       
        
        [HttpPut("{ain}")]
        public bool Put(int ain, [FromBody] int start)
        {
            List<OptionSellOnRSICross> activeAlgoObjects;
            if (!_cache.TryGetValue(key, out activeAlgoObjects))
            {
                activeAlgoObjects = new List<OptionSellOnRSICross>();
            }

            OptionSellOnRSICross algoObject = activeAlgoObjects.FirstOrDefault(x => x.AlgoInstance == ain);
            if (algoObject != null)
            {
                algoObject.StopTrade(!Convert.ToBoolean(start));
            }
            _cache.Set(key, activeAlgoObjects);

            return true;
        }

        // GET: api/<RSIStrangleController>
        //[HttpGet]
        //public void Get()
        //{
        //    StartService();
        //}

        //        [HttpGet("startservice")]
        //        public void StartService()
        //        {
        //            //BNF PE/ CE
        //            uint instrumentToken = 260105;
        //            DateTime endDateTime = DateTime.Now;
        //            TimeSpan candleTimeSpan = new TimeSpan(1, 15, 0);

        //            DateTime? expiry = Convert.ToDateTime("2020-10-29");
        //            //decimal strikePriceRange = 1;

        //#if local
        //            endDateTime = Convert.ToDateTime("2020-10-29 09:15:00");
        //#endif
        //            OptionSellOnRSICross expiryTrade = new OptionSellOnRSICross(candleTimeSpan, instrumentToken, endDateTime, expiry);
        //            expiryTrade.OnOptionUniverseChange += ExpiryTrade_OnOptionUniverseChange;

        //            List<uint> tokens = new List<uint>();
        //            tokens.Add(instrumentToken);
        //            zmqClient = new ZMQClient();
        //            zmqClient.AddSubscriber(tokens);
        //            zmqClient.Subscribe(expiryTrade);
        //        }

        private void ExpiryTrade_OnOptionUniverseChange(OptionSellOnRSICross source)
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
            return Task.FromResult((int)AlgoIndex.SellOnRSICross);
        }

        //// GET api/<RSICrossController>/5
        //[HttpGet("{id}")]
        //public string Get(int id)
        //{
        //    return "value";
        //}

        //// POST api/<RSICrossController>
        //[HttpPost]
        //public void Post([FromBody] string value)
        //{
        //}

        //// PUT api/<RSICrossController>/5
        //[HttpPut("{id}")]
        //public void Put(int id, [FromBody] string value)
        //{
        //}

        //// DELETE api/<RSICrossController>/5
        //[HttpDelete("{id}")]
        //public void Delete(int id)
        //{
        //}
    }
}
