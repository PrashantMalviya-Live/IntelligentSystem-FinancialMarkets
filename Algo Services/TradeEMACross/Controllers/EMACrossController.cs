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

namespace TradeEMACross.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EMACrossController : Controller
    {
        private const string key = "ACTIVE_EMACROSS_OBJECTS";
        IConfiguration configuration;
        ZMQClient zmqClient;
        private IMemoryCache _cache;
        public EMACrossController(IMemoryCache cache)
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
            DataSet ds = dl.GetActiveAlgos(AlgoIndex.EMACross);

            List<ActiveAlgosView> activeAlgos = new List<ActiveAlgosView>();

            DataTable dtActiveAlgos = ds.Tables[0];
            DataRelation algo_orders_relation = ds.Relations.Add("Algo_Orders", new DataColumn[] { ds.Tables[0].Columns["Id"] },
                new DataColumn[] { ds.Tables[2].Columns["AlgoInstance"] });

            foreach (DataRow drAlgo in dtActiveAlgos.Rows)
            {
                ActiveAlgosView algosView = new ActiveAlgosView();

                OptionBuyOnEMACrossInputs algoInput = new OptionBuyOnEMACrossInputs
                {
                    Expiry = Convert.ToDateTime(drAlgo["Expiry"]),
                    CTF = Convert.ToInt32(drAlgo["CandleTimeFrame_Mins"]),
                    Qty = Convert.ToInt32(drAlgo["InitialQtyInLotSize"]),
                    BToken = Convert.ToUInt32(drAlgo["BToken"]),
                    SEMA = Convert.ToInt32(DBNull.Value != drAlgo["Arg1"] ? drAlgo["Arg1"] : 0),
                    LEMA = Convert.ToInt32(DBNull.Value != drAlgo["Arg2"] ? drAlgo["Arg2"] : 0),
                    TP = Convert.ToDecimal(DBNull.Value != drAlgo["Arg3"] ? drAlgo["Arg3"] : 0),
                    SL = Convert.ToDecimal(DBNull.Value != drAlgo["Arg4"] ? drAlgo["Arg4"] : 0),
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
        public async Task<ActiveAlgosView> Trade([FromBody] OptionBuyOnEMACrossInputs optionBuyOnEMACrossInputs, int algoInstance = 0)
        {
            uint instrumentToken = optionBuyOnEMACrossInputs.BToken;
            DateTime endDateTime = DateTime.Now;
            TimeSpan candleTimeSpan = TimeSpan.FromMinutes(optionBuyOnEMACrossInputs.CTF);

            DateTime expiry = optionBuyOnEMACrossInputs.Expiry;
            int optionQuantity = optionBuyOnEMACrossInputs.Qty;

#if local
            endDateTime = Convert.ToDateTime("2020-11-09 09:15:00");
#endif

            ///FOR ALL STOCKS FUTURE , PASS INSTRUMENTTOKEN AS ZERO. FOR CE/PE ON BNF/NF SEND THE INDEX TOKEN AS INSTRUMENTTOKEN
            OptionBuyonEMACross optionBuyonEMACross =
                new OptionBuyonEMACross(candleTimeSpan, instrumentToken, //expiry,
                optionQuantity, uid:"PM27031981", optionBuyOnEMACrossInputs.TP, optionBuyOnEMACrossInputs.SL, 
                optionBuyOnEMACrossInputs.SEMA, optionBuyOnEMACrossInputs.LEMA, 200, algoInstance, false, 0);

            optionBuyonEMACross.OnOptionUniverseChange += OptionBuywithRSI_OnOptionUniverseChange;
            optionBuyonEMACross.OnTradeEntry += OptionSellwithRSI_OnTradeEntry;
            optionBuyonEMACross.OnTradeExit += OptionSellwithRSI_OnTradeExit;

            List<OptionBuyonEMACross> activeAlgoObjects = _cache.Get<List<OptionBuyonEMACross>>(key);

            if (activeAlgoObjects == null)
            {
                activeAlgoObjects = new List<OptionBuyonEMACross>();
            }
            activeAlgoObjects.Add(optionBuyonEMACross);
            _cache.Set(key, activeAlgoObjects);


            Task task = Task.Run(() => NMQClientSubscription(optionBuyonEMACross, instrumentToken));

            //await task;
            return new ActiveAlgosView
            {
                aid = Convert.ToInt32(AlgoIndex.EMACross),
                an = Convert.ToString((AlgoIndex)AlgoIndex.EMACross),
                ains = optionBuyonEMACross.AlgoInstance,
                algodate = endDateTime.ToString("yyyy-MM-dd"),
                binstrument = instrumentToken.ToString(),
                expiry = expiry.ToString("yyyy-MM-dd"),
                lotsize = optionQuantity,
                mins = optionBuyOnEMACrossInputs.CTF
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

        private async Task NMQClientSubscription(OptionBuyonEMACross optionBuyWithRSI, uint token)
        {
            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(new List<uint>() { token });

            await zmqClient.Subscribe(optionBuyWithRSI);
        }

        private void OptionBuywithRSI_OnOptionUniverseChange(OptionBuyonEMACross source)
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
            return Task.FromResult((int)AlgoIndex.EMACross);
        }
        [HttpPut("{ain}")]
        public bool Put(int ain, [FromBody] int start)
        {
            List<OptionBuyonEMACross> activeAlgoObjects;
            if (!_cache.TryGetValue(key, out activeAlgoObjects))
            {
                activeAlgoObjects = new List<OptionBuyonEMACross>();
            }

            OptionBuyonEMACross algoObject = activeAlgoObjects.FirstOrDefault(x => x.AlgoInstance == ain);
            if (algoObject != null)
            {
                algoObject.StopTrade(!Convert.ToBoolean(start));
            }
            _cache.Set(key, activeAlgoObjects);

            return true;
        }

        //// GET api/<EMACrossController>/5
        //[HttpGet("{id}")]
        //public string Get(int id)
        //{
        //    return "value";
        //}

        //// POST api/<EMACrossController>
        //[HttpPost]
        //public void Post([FromBody] string value)
        //{
        //}

        //// PUT api/<EMACrossController>/5
        //[HttpPut("{id}")]
        //public void Put(int id, [FromBody] string value)
        //{
        //}

        //// DELETE api/<EMACrossController>/5
        //[HttpDelete("{id}")]
        //public void Delete(int id)
        //{
        //}
    }
}
