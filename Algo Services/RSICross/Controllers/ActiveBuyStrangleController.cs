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

namespace RSICross.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ActiveBuyStrangleController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private const string key = "ACTIVE_BUY_STRANGLE_OBJECTS";
        IConfiguration configuration;
        ZMQClient zmqClient;
        private IMemoryCache _cache;
        public ActiveBuyStrangleController(IMemoryCache cache, IHttpClientFactory httpClientFactory)
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

        [HttpGet("activealgos")]
        public async Task<IEnumerable<ActiveAlgosView>> GetActiveAlgos()
        {
            try
            {
                string userId = "PM27031981";
                DataLogic dl = new DataLogic();
                DataSet ds = dl.GetActiveAlgos(AlgoIndex.ActiveTradeWithVariableQty, userId);

                List<ActiveAlgosView> activeAlgos = new List<ActiveAlgosView>();

                DataTable dtActiveAlgos = ds.Tables[0];
                DataRelation algo_orders_relation = ds.Relations.Add("Algo_Orders", new DataColumn[] { ds.Tables[0].Columns["Id"] },
                    new DataColumn[] { ds.Tables[2].Columns["AlgoInstance"] });

                DataRelation algo_orderTrios_relation = ds.Relations.Add("Algo_OrderTrios", new DataColumn[] { ds.Tables[0].Columns["Id"] },
                    new DataColumn[] { ds.Tables[3].Columns["AlgoInstance"] });

                //DataRelation orderTrios_orders_relation = ds.Relations.Add("OrderTrios_Orders", new DataColumn[] { ds.Tables[3].Columns["MainOrderId"] },
                //    new DataColumn[] { ds.Tables[2].Columns["OrderId"] });

                foreach (DataRow drAlgo in dtActiveAlgos.Rows)
                {
                    ActiveAlgosView algosView = new ActiveAlgosView();

                    StrangleWithDeltaandLevelInputs algoInput = new StrangleWithDeltaandLevelInputs
                    {
                        Expiry = Convert.ToDateTime(drAlgo["Expiry"]),
                        CTF = Convert.ToInt32(drAlgo["CandleTimeFrame_Mins"]),
                        IQty = Convert.ToInt32(drAlgo["InitialQtyInLotSize"]),
                        MaxQty = Convert.ToInt32(drAlgo["MaxQtyInLotSize"]),
                        StepQty = Convert.ToInt32(drAlgo["StepQtyInLotSize"]),
                        MinQty = Convert.ToInt32(drAlgo["lowerLimit"]),

                        BToken = Convert.ToUInt32(drAlgo["BToken"]),
                        UID = Convert.ToString(DBNull.Value != drAlgo["Arg9"] ? drAlgo["Arg9"] : 0),
                        //PS = Convert.ToBoolean(DBNull.Value != drAlgo["PositionSizing"] ? drAlgo["PositionSizing"] : false),
                        TP = Convert.ToDecimal(DBNull.Value != drAlgo["Arg1"] ? drAlgo["Arg1"] : 0),
                        SL = Convert.ToDecimal(DBNull.Value != drAlgo["Arg2"] ? drAlgo["Arg2"] : 0),
                        //IntD = Convert.ToDecimal(DBNull.Value != drAlgo["Arg3"] ? drAlgo["Arg3"] : 0) == 1,
                        PnL = Convert.ToDecimal(DBNull.Value != drAlgo["NetPrice"] ? drAlgo["NetPrice"] : 0)
                    };
                    algosView.aid = Convert.ToInt32(drAlgo["AlgoId"]);
                    algosView.an = Convert.ToString((AlgoIndex)algosView.aid);
                    algosView.ains = Convert.ToInt32(drAlgo["Id"]);

                    algosView.expiry = Convert.ToDateTime(drAlgo["Expiry"]).ToString("yyyy-MM-dd");
                    algosView.mins = Convert.ToInt32(drAlgo["CandleTimeFrame_Mins"]);
                    algosView.lotsize = Convert.ToInt32(drAlgo["InitialQtyInLotSize"]);
                    algosView.binstrument = Convert.ToString(drAlgo["BToken"]);
                    algosView.algodate = Convert.ToDateTime(drAlgo["Timestamp"]).ToString("yyyy-MM-dd");

                    DataRow[] drOrderTrios = drAlgo.GetChildRows(algo_orderTrios_relation);

                    //algoInput.CallOrder ??= new OrderTrio();
                    List<OrderTrio> orderTrios = new List<OrderTrio>();
                    foreach (DataRow drOrderTrio in drOrderTrios)
                    {
                        if (Convert.ToBoolean(drOrderTrio["Active"]))
                        {
                            OrderTrio orderTrio = new OrderTrio()
                            {
                                Id = Convert.ToInt32(drOrderTrio["Id"]),
                                //TargetProfit = Convert.ToDecimal(drOrderTrio["TargetProfit"]),
                                //StopLoss = Convert.ToDecimal(drOrderTrio["StopLoss"])
                            };

                            DataRow[] drOrders = drAlgo.GetChildRows(algo_orders_relation);
                            foreach (DataRow drOrder in drOrders)
                            {
                                if (Convert.ToString(drOrderTrio["MainOrderId"]) == Convert.ToString(drOrder["OrderId"]))
                                {
                                    Order o = ViewUtility.GetOrder(drOrder);

                                    switch (o.OrderType.ToUpper())
                                    {
                                        case Constants.ORDER_TYPE_MARKET:
                                            orderTrio.Order = o;
                                            break;

                                        case Constants.ORDER_TYPE_SLM:
                                            orderTrio.TPOrder = o;
                                            break;
                                    }
                                    algosView.Orders.Add(ViewUtility.GetOrderView(o));
                                }
                            }
                            //Instrument option = dl.GetInstrument(orderTrio.Order.Tradingsymbol);
                            //orderTrio.Option = option;
                            orderTrios.Add(orderTrio);
                        }
                    }
                    //activeAlgos.Add(algosView);
                    algoInput.ActiveOrderTrios = orderTrios;

                    Trade(algoInput, algosView.ains);

                    //if (algosView.Orders != null)
                    //{
                    activeAlgos.Add(algosView);
                    //}
                }
                return activeAlgos.ToArray();
            }
            catch (Exception ex)
            {
                return null;
            }
        }
        [HttpPost]
        public async Task<ActiveAlgosView> Trade([FromBody] StrangleWithDeltaandLevelInputs strangleWithDeltaandLevelInputs, int algoInstance = 0)
        {
            uint instrumentToken = strangleWithDeltaandLevelInputs.BToken;
            //DateTime endDateTime = DateTime.Now;
            //TimeSpan candleTimeSpan = TimeSpan.FromMinutes(optionBuyWithStraddleInput.CTF);

            DateTime expiry = strangleWithDeltaandLevelInputs.Expiry;
            //int optionQuantity = optionBuyWithStraddleInput.Qty;
            strangleWithDeltaandLevelInputs.IQty = 10;
            strangleWithDeltaandLevelInputs.StepQty = 4;
            strangleWithDeltaandLevelInputs.MaxQty = 18;

            string userid = "PM27031981";

#if local
            //endDateTime = Convert.ToDateTime("2020-10-16 12:21:00");
#endif
            ///FOR ALL STOCKS FUTURE , PASS INSTRUMENTTOKEN AS ZERO. FOR CE/PE ON BNF/NF SEND THE INDEX TOKEN AS INSTRUMENTTOKEN
            ActiveBuyStrangleManagerWithVariableQty straddleManager = new ActiveBuyStrangleManagerWithVariableQty(instrumentToken, expiry,
            strangleWithDeltaandLevelInputs.IQty, strangleWithDeltaandLevelInputs.StepQty, strangleWithDeltaandLevelInputs.MaxQty, strangleWithDeltaandLevelInputs.TP,
            strangleWithDeltaandLevelInputs.SL, strangleWithDeltaandLevelInputs.IDelta, strangleWithDeltaandLevelInputs.MaxDelta, strangleWithDeltaandLevelInputs.MinDelta,
            AlgoIndex.ActiveTradeWithVariableQty, _httpClientFactory, algoInstance, userid: userid);

            //straddleManager.LoadActiveOrders(strangleWithDeltaandLevelInputs.ActiveOrderTrios);

            straddleManager.OnOptionUniverseChange += ExpiryTrade_OnOptionUniverseChange;
            straddleManager.OnTradeEntry += OptionSellwithRSI_OnTradeEntry;
            straddleManager.OnTradeExit += OptionSellwithRSI_OnTradeExit;

            List<ActiveBuyStrangleManagerWithVariableQty> activeAlgoObjects = _cache.Get<List<ActiveBuyStrangleManagerWithVariableQty>>(key);

            if (activeAlgoObjects == null)
            {
                activeAlgoObjects = new List<ActiveBuyStrangleManagerWithVariableQty>();
            }
            activeAlgoObjects.Add(straddleManager);
            _cache.Set(key, activeAlgoObjects);

            Task task = Task.Run(() => NMQClientSubscription(straddleManager, instrumentToken));


            //await task;
            return new ActiveAlgosView
            {
                aid = Convert.ToInt32(AlgoIndex.ActiveTradeWithVariableQty),
                an = Convert.ToString((AlgoIndex)AlgoIndex.ActiveTradeWithVariableQty),
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

        private async Task NMQClientSubscription(ActiveBuyStrangleManagerWithVariableQty straddleManager, uint token)
        {
            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(new List<uint>() { token });

            await zmqClient.Subscribe(straddleManager);
        }

        private void ExpiryTrade_OnOptionUniverseChange(ActiveBuyStrangleManagerWithVariableQty source)
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
            return Task.FromResult((int)AlgoIndex.ActiveTradeWithVariableQty);
        }

        [HttpPut("{ain}")]
        public bool Put(int ain, [FromBody] int start)
        {
            List<ActiveBuyStrangleManagerWithVariableQty> activeAlgoObjects;
            if (!_cache.TryGetValue(key, out activeAlgoObjects))
            {
                activeAlgoObjects = new List<ActiveBuyStrangleManagerWithVariableQty>();
            }

            ActiveBuyStrangleManagerWithVariableQty algoObject = activeAlgoObjects.FirstOrDefault(x => x.AlgoInstance == ain);
            if (algoObject != null)
            {
                algoObject.StopTrade(!Convert.ToBoolean(start));
            }
            _cache.Set(key, activeAlgoObjects);

            return true;
        }

    }
}
