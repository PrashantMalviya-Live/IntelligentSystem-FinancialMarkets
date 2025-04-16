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

namespace DeltaManagerService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OptionStrangleBuyController : ControllerBase
    {
        private const string key = "ACTIVE_OPTIONSTRANGLEBUY_OBJECTS";
        IConfiguration configuration;
        ZMQClient zmqClient;
        private IMemoryCache _cache;
        public OptionStrangleBuyController(IMemoryCache cache)
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
            DataSet ds = dl.GetActiveAlgos(AlgoIndex.MomentumBuyWithRSI);

            List<ActiveAlgosView> activeAlgos = new List<ActiveAlgosView>();

            DataTable dtActiveAlgos = ds.Tables[0];
            DataRelation algo_orders_relation = ds.Relations.Add("Algo_Orders", new DataColumn[] { ds.Tables[0].Columns["Id"] },
                new DataColumn[] { ds.Tables[2].Columns["AlgoInstance"] });

            foreach (DataRow drAlgo in dtActiveAlgos.Rows)
            {
                ActiveAlgosView algosView = new ActiveAlgosView();

                OptionBuywithRSIInput algoInput = new OptionBuywithRSIInput
                {
                    Expiry = Convert.ToDateTime(drAlgo["Expiry"]),
                    CTF = Convert.ToInt32(drAlgo["CandleTimeFrame_Mins"]),
                    Qty = Convert.ToInt32(drAlgo["InitialQtyInLotSize"]),
                    BToken = Convert.ToUInt32(drAlgo["BToken"]),
                    MinDFBI = Convert.ToDecimal(DBNull.Value != drAlgo["Arg5"] ? drAlgo["Arg5"] : 0),
                    MaxDFBI = Convert.ToDecimal(DBNull.Value != drAlgo["Arg4"] ? drAlgo["Arg4"] : 0),
                    //RULX = Convert.ToDecimal(DBNull.Value != drAlgo["UpperLimit"] ? drAlgo["UpperLimit"] : 0),
                    //RLLE = Convert.ToDecimal(DBNull.Value != drAlgo["LowerLimit"] ? drAlgo["LowerLimit"] : 0),
                    //EMA = Convert.ToInt32(DBNull.Value != drAlgo["Arg1"] ? drAlgo["Arg1"] : 0),
                    //PS = Convert.ToBoolean(DBNull.Value != drAlgo["PositionSizing"] ? drAlgo["PositionSizing"] : false),
                    TP = Convert.ToDecimal(DBNull.Value != drAlgo["Arg2"] ? drAlgo["Arg2"] : 0),
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

                var slmOrders = orders.Where(o => o.Status == Constants.ORDER_STATUS_TRIGGER_PENDING && o.OrderType == Constants.ORDER_TYPE_SLM);
                var completedOrders = orders.Where(o => o.Status == Constants.ORDER_STATUS_COMPLETE);

                var pendingOrders = slmOrders.Where(x => !completedOrders.Any(c => c.OrderId == x.OrderId));

                //if (pendingOrders != null && pendingOrders.Count() > 0)
                //{
                //    algoInput.ActiveOrder = pendingOrders.FirstOrDefault();
                //    algosView.Orders.Add(ViewUtility.GetOrderView(pendingOrders.FirstOrDefault()));
                //    if (algoInput.ActiveOrder != null)
                //    {
                //        Trade(algoInput, algosView.ains);
                //    }
                //    activeAlgos.Add(algosView);
                //}
            }
            return activeAlgos.ToArray();
        }


        [HttpPost]
        public async Task<ActiveAlgosView> Trade([FromBody] OptionBuywithRSIInput optionBuywithRSIInput, int algoInstance = 0)
        {
            uint instrumentToken = optionBuywithRSIInput.BToken;
            DateTime endDateTime = DateTime.Now;
            TimeSpan candleTimeSpan = TimeSpan.FromMinutes(optionBuywithRSIInput.CTF);

            DateTime expiry = optionBuywithRSIInput.Expiry;
            int optionQuantity = optionBuywithRSIInput.Qty;

#if local
            endDateTime = Convert.ToDateTime("2020-11-09 09:15:00");
#endif

            ///FOR ALL STOCKS FUTURE , PASS INSTRUMENTTOKEN AS ZERO. FOR CE/PE ON BNF/NF SEND THE INDEX TOKEN AS INSTRUMENTTOKEN
            OptionBuyPremiumCross optionBuywithRSI =
                new OptionBuyPremiumCross(endDateTime, candleTimeSpan, instrumentToken, expiry,
                optionQuantity, optionBuywithRSIInput.MinDFBI, optionBuywithRSIInput.MaxDFBI,
                optionBuywithRSIInput.RLLE, optionBuywithRSIInput.RULX, ceLevel:0, peLevel:0,
                targetProfit: optionBuywithRSIInput.TP, optionBuywithRSIInput.EMA,
                algoInstance: algoInstance, positionSizing: false, maxLossPerTrade: 0);

            optionBuywithRSI.OnOptionUniverseChange += OptionBuywithRSI_OnOptionUniverseChange;
            optionBuywithRSI.OnTradeEntry += OptionSellwithRSI_OnTradeEntry;
            optionBuywithRSI.OnTradeExit += OptionSellwithRSI_OnTradeExit;

            List<OptionBuyPremiumCross> activeAlgoObjects = _cache.Get<List<OptionBuyPremiumCross>>(key);

            if (activeAlgoObjects == null)
            {
                activeAlgoObjects = new List<OptionBuyPremiumCross>();
            }
            activeAlgoObjects.Add(optionBuywithRSI);
            _cache.Set(key, activeAlgoObjects);


            Task task = Task.Run(() => NMQClientSubscription(optionBuywithRSI, instrumentToken));

            //await task;
            return new ActiveAlgosView
            {
                aid = Convert.ToInt32(AlgoIndex.MomentumBuyWithRSI),
                an = Convert.ToString((AlgoIndex)AlgoIndex.MomentumBuyWithRSI),
                ains = optionBuywithRSI.AlgoInstance,
                algodate = endDateTime.ToString("yyyy-MM-dd"),
                binstrument = instrumentToken.ToString(),
                expiry = expiry.ToString("yyyy-MM-dd"),
                lotsize = optionQuantity,
                mins = optionBuywithRSIInput.CTF
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

        private async Task NMQClientSubscription(OptionBuyPremiumCross buyWithRSI, uint token)
        {
            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(new List<uint>() { token });

            await zmqClient.Subscribe(buyWithRSI);
        }

        private void OptionBuywithRSI_OnOptionUniverseChange(OptionBuyPremiumCross source)
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
            return Task.FromResult((int)AlgoIndex.MomentumBuyWithRSI);
        }
        [HttpPut("{ain}")]
        public bool Put(int ain, [FromBody] int start)
        {
            List<OptionBuyPremiumCross> activeAlgoObjects;
            if (!_cache.TryGetValue(key, out activeAlgoObjects))
            {
                activeAlgoObjects = new List<OptionBuyPremiumCross>();
            }

            OptionBuyPremiumCross algoObject = activeAlgoObjects.FirstOrDefault(x => x.AlgoInstance == ain);
            if (algoObject != null)
            {
                algoObject.StopTrade(!Convert.ToBoolean(start));
            }
            _cache.Set(key, activeAlgoObjects);

            return true;
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
