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

namespace RSIManagerService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RSIStrangleController : ControllerBase
    {
        private const string key = "ACTIVE_RSISELLSTRANGLE_OBJECTS";
        IConfiguration configuration;
        ZMQClient zmqClient;
        private IMemoryCache _cache;
        public RSIStrangleController(IMemoryCache cache)
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
            DataSet ds = dl.GetActiveAlgos(AlgoIndex.StrangleWithRSI);

            List<ActiveAlgosView> activeAlgos = new List<ActiveAlgosView>();

            DataTable dtActiveAlgos = ds.Tables[0];
            DataRelation algo_orders_relation = ds.Relations.Add("Algo_Orders", new DataColumn[] { ds.Tables[0].Columns["Id"] },
                new DataColumn[] { ds.Tables[2].Columns["AlgoInstance"] });

            foreach (DataRow drAlgo in dtActiveAlgos.Rows)
            {
                ActiveAlgosView algosView = new ActiveAlgosView();

                OptionSellwithRSIInput algoInput = new OptionSellwithRSIInput
                {
                    Expiry = Convert.ToDateTime(drAlgo["Expiry"]),
                    CTF = Convert.ToInt32(drAlgo["CandleTimeFrame_Mins"]),
                    Qty = Convert.ToInt32(drAlgo["InitialQtyInLotSize"]),
                    BToken = Convert.ToUInt32(drAlgo["BToken"]),
                    //PS = Convert.ToBoolean(DBNull.Value != drAlgo["PositionSizing"] ? drAlgo["PositionSizing"] : false),
                    MinDFBI = Convert.ToDecimal(DBNull.Value != drAlgo["Arg5"] ? drAlgo["Arg5"] : 0),
                    MaxDFBI = Convert.ToDecimal(DBNull.Value != drAlgo["Arg4"] ? drAlgo["Arg4"] : 0),
                    RLX = Convert.ToDecimal(DBNull.Value != drAlgo["Arg3"] ? drAlgo["Arg3"] : 0),
                    RMX = Convert.ToDecimal(DBNull.Value != drAlgo["Arg2"] ? drAlgo["Arg2"] : 0),
                    RULE = Convert.ToDecimal(DBNull.Value != drAlgo["UpperLimit"] ? drAlgo["UpperLimit"] : 0),
                    RLLE = Convert.ToDecimal(DBNull.Value != drAlgo["LowerLimit"] ? drAlgo["LowerLimit"] : 0),
                    EMA = Convert.ToInt32(DBNull.Value != drAlgo["Arg1"] ? drAlgo["Arg1"] : 0),
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

                //var slmOrders = orders.Where(o => o.Status == Constants.ORDER_STATUS_TRIGGER_PENDING && o.OrderType == Constants.ORDER_TYPE_SLM);

                var lastExecutedOrders = orders.Where(o => o.Status.ToUpper() == Constants.ORDER_STATUS_COMPLETE
                && o.OrderType.ToUpper() != Constants.ORDER_TYPE_SLM
                && (o.Tag == null || o.Tag.ToUpper() != "NW")).OrderByDescending(x => x.OrderTimestamp);

                var lastOrder = lastExecutedOrders.FirstOrDefault();

                if (lastOrder != null)
                {
                    string optionType = string.Concat(lastOrder.Tradingsymbol.Trim(' ').TakeLast(2));


                    if (lastOrder.TransactionType.ToLower() == "sell")
                    {
                        algosView.Orders.Add(ViewUtility.GetOrderView(lastOrder));

                        if (optionType.ToLower() == "ce")
                        {
                            algoInput.CallOrder = lastOrder;
                        }
                        else 
                        {
                            algoInput.PutOrder = lastOrder;
                        }

                    }
                    optionType = optionType.ToLower() == "ce" ? "pe" : "ce";
                    
                    var lastOrderOfOppositeType = lastExecutedOrders.FirstOrDefault(x => string.Concat(x.Tradingsymbol.Trim(' ').TakeLast(2)) == optionType);
                    if(lastOrderOfOppositeType != null && lastOrderOfOppositeType.TransactionType.ToLower() == "sell")
                    {
                        if (optionType.ToLower() == "ce")
                        {
                            algoInput.CallOrder = lastOrderOfOppositeType;
                        }
                        else
                        {
                            algoInput.PutOrder = lastOrderOfOppositeType;
                        }
                        algosView.Orders.Add(ViewUtility.GetOrderView(lastOrderOfOppositeType));
                    }
                    Trade(algoInput, algosView.ains);
                }

                if (algosView.Orders != null)
                {
                    activeAlgos.Add(algosView);
                }

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
        public async Task<ActiveAlgosView> Trade([FromBody] OptionSellwithRSIInput optionSellwithRSIInput, int algoInstance = 0)
        {
            uint instrumentToken = optionSellwithRSIInput.BToken;
            DateTime endDateTime = DateTime.Now;
            TimeSpan candleTimeSpan = TimeSpan.FromMinutes(optionSellwithRSIInput.CTF);

            DateTime expiry = optionSellwithRSIInput.Expiry;
            //decimal strikePriceRange = 1;
            int optionQuantity = optionSellwithRSIInput.Qty;

#if local
            endDateTime = Convert.ToDateTime("2020-10-16 12:21:00");
#endif
            ///FOR ALL STOCKS FUTURE , PASS INSTRUMENTTOKEN AS ZERO. FOR CE/PE ON BNF/NF SEND THE INDEX TOKEN AS INSTRUMENTTOKEN
            OptionSellingWithRSI optionSellwithRSI =
                new OptionSellingWithRSI(endDateTime, candleTimeSpan, instrumentToken, expiry,
                optionQuantity, optionSellwithRSIInput.MinDFBI, optionSellwithRSIInput.MaxDFBI, optionSellwithRSIInput.EMA,
                algoInstance, false, 0, rsiLowerLimitForEntry: optionSellwithRSIInput.RLLE,
                rsiUpperLimitForEntry: optionSellwithRSIInput.RULE, rsiMarginForExit: 
                optionSellwithRSIInput.RMX, rsiLimitForExit: optionSellwithRSIInput.RLX);

            optionSellwithRSI.LoadActiveOrders(optionSellwithRSIInput.CallOrder, optionSellwithRSIInput.PutOrder);

            optionSellwithRSI.OnOptionUniverseChange += ExpiryTrade_OnOptionUniverseChange;
            optionSellwithRSI.OnTradeEntry += OptionSellwithRSI_OnTradeEntry;
            optionSellwithRSI.OnTradeExit += OptionSellwithRSI_OnTradeExit;
                        
            List<OptionSellingWithRSI> activeAlgoObjects = _cache.Get<List<OptionSellingWithRSI>>(key);

            if(activeAlgoObjects == null)
            {
                activeAlgoObjects = new List<OptionSellingWithRSI>();
            }
            activeAlgoObjects.Add(optionSellwithRSI);
            _cache.Set(key, activeAlgoObjects);

            Task task = Task.Run(() => NMQClientSubscription(optionSellwithRSI, instrumentToken));

            //await task;
            return new ActiveAlgosView
            {
                aid = Convert.ToInt32(AlgoIndex.StrangleWithRSI),
                an = Convert.ToString((AlgoIndex)AlgoIndex.StrangleWithRSI),
                ains = optionSellwithRSI.AlgoInstance,
                algodate = endDateTime.ToString("yyyy-MM-dd"),
                binstrument = instrumentToken.ToString(),
                expiry = expiry.ToString("yyyy-MM-dd"),
                lotsize = optionQuantity,
                mins = optionSellwithRSIInput.CTF
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

        private async Task NMQClientSubscription(OptionSellingWithRSI sellOnRSICross, uint token)
        {
            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(new List<uint>() { token });

            await zmqClient.Subscribe(sellOnRSICross);
        }

        private void ExpiryTrade_OnOptionUniverseChange(OptionSellingWithRSI source)
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
            return Task.FromResult((int)AlgoIndex.StrangleWithRSI);
        }

        [HttpPut("{ain}")]
        public bool Put(int ain, [FromBody] int start)
        {
            List<OptionSellingWithRSI> activeAlgoObjects;
            if(!_cache.TryGetValue(key, out activeAlgoObjects))
            {
                activeAlgoObjects = new List<OptionSellingWithRSI>();
            }

            OptionSellingWithRSI algoObject = activeAlgoObjects.FirstOrDefault(x => x.AlgoInstance == ain);
            if(algoObject != null)
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



        //// DELETE api/<RSICrossController>/5
        //[HttpDelete("{id}")]
        //public void Delete(int id)
        //{
        //}
    }
}
