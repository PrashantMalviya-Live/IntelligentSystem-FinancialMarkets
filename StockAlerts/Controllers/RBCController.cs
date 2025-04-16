using Algos.TLogics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using ZMQFacade;
//using KafkaFacade;
//using MQTTFacade;
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
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using FirebaseAdmin.Messaging;
using Newtonsoft.Json;
using System.Net;
using System.IO;
using Google.Apis.Logging;
//using LocalDBData;

namespace StockAlerts.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RBCController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private const string key = "ACTIVE_BreakoutCandles_OBJECT";
        private const string SERVER_API_KEY = @"AAAA0HFQ8ag:APA91bFyK2buMq1eStQ2vh4ax83NkB-NVkax04BvlEX7G9iYTaJIGyayq8MLcPHQHDjXc1YqB5BxMnSghpToKtbmMRARJLFJuamLm6-7qgqFD91moXQLz_xJOb0dVR_Chbt8y8tkNTqo";
        private const string SENDER_ID = "895254327720";
        private const string registrationToken = "dhovn1Sk88c:APA91bFuHzKpxeKbfn-5oQG4xZ54uKTzGAzj7vqGs61RwYm_D1irujGL1U64npy2O1Yt8cKGaS11q7KXu9HGltbnSrDA4roCNRvUDSdTAy0DhabU2Br0aQTkrOAc2Z8j6cqSmv7rECEu";

        IConfiguration configuration;
        ZMQClient zmqClient;
        //KSubscriber kfkClient;
        //Publisher mqttPublisher;
        //MQTTSubscriber mqttSubscriber;
        private IMemoryCache _cache;
        public RBCController(IMemoryCache cache, IHttpClientFactory httpClientFactory)
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

        //[HttpGet("{token}")]
        //public IEnumerable<string> OptionExpiries(uint token)
        //{
        //    DataLogic dl = new DataLogic();
        //    List<string> expiryList = dl.RetrieveOptionExpiries(token);
        //    return expiryList;
        //}
        [HttpGet("{token}")]
        public IEnumerable<DateTime> FutureExpiries(uint token)
        {
            DataLogic dl = new DataLogic();
            List<DateTime> expiryList = new() { dl.GetCurrentMonthlyExpiry(DateTime.Today, token) };
            return expiryList;
        }



        [HttpPost]
        public async Task<ActiveAlgosView> Trade([FromBody] PriceActionInput paInputs, int algoInstance = 0)
        {
            RangeBreakoutCandle paTrader;
            PriceActionInput input = GetActiveAlgos();
            if (input == null)
            {
                paTrader = await ExecuteAlgo(paInputs, algoInstance);
            }
            else
            {
                paTrader = await ExecuteAlgo(input, algoInstance);
                paTrader.LoadActiveOrders(input);
            }

            //RangeBreakoutCandle paTrader = await ExecuteAlgo(paInputs, algoInstance);

            paTrader.StopTrade(false);
            paTrader.OnOptionUniverseChange += PATrade_OnOptionUniverseChange;
            paTrader.OnCriticalEvents += SendNotification;// (string title, string body)
            paTrader.OnTradeEntry += BC_OnTradeEntry;
            paTrader.OnTradeExit += BC_OnTradeExit;

            List<RangeBreakoutCandle> activeAlgoObjects = _cache.Get<List<RangeBreakoutCandle>>(key);

            if (activeAlgoObjects == null)
            {
                activeAlgoObjects = new List<RangeBreakoutCandle>();
            }
            activeAlgoObjects.Add(paTrader);
            _cache.Set(key, activeAlgoObjects);

            Task task = Task.Run(() => NMQClientSubscription(paTrader, paInputs.BToken));
#if local
            //Task observerSubscriptionTask = Task.Run(() => ObserverSubscription(paTrader, paInputs.BToken));
            //Task kftask = Task.Run(() => KFKClientSubscription(paTrader, paInputs.BToken));
#endif

           // LoggerCore.PublishLog(1, AlgoIndex.BC, GlobalLayer.LogLevel.Debug, DateTime.Now, "Algo has started", "Controller");

            //await task;
            return new ActiveAlgosView
            {
                aid = Convert.ToInt32(AlgoIndex.BC),
                an = Convert.ToString((AlgoIndex)AlgoIndex.BC),
                ains = paTrader.AlgoInstance,
                algodate = DateTime.Now.ToString("yyyy-MM-dd"),
                binstrument = paInputs.BToken.ToString(),
                expiry = paInputs.Expiry.ToString("yyyy-MM-dd"),
                lotsize = paInputs.Qty,
                mins = paInputs.CTF
            };

            
        }

        [HttpGet("{token}/{algoindex}")]
        public int GetLog(uint token, int algoIndex)
        {
            LoggerCore.PublishLog(1, AlgoIndex.BC, GlobalLayer.LogLevel.Debug, DateTime.Now, "Algo has started", "Controller");
            return 1;
        }

        private PriceActionInput GetActiveAlgos()
        {
            try
            {
                string userId = "client1188";
                DataLogic dl = new DataLogic();
                DataSet ds = dl.GetActiveAlgos(AlgoIndex.RangeBreakoutCandle, userId);


                DataTable dtActiveAlgos = ds.Tables[0];
                DataRelation algo_orders_relation = ds.Relations.Add("Algo_Orders", new DataColumn[] { ds.Tables[0].Columns["Id"] },
                    new DataColumn[] { ds.Tables[2].Columns["AlgoInstance"] });

                DataRelation algo_orderTrios_relation = ds.Relations.Add("Algo_OrderTrios", new DataColumn[] { ds.Tables[0].Columns["Id"] },
                    new DataColumn[] { ds.Tables[3].Columns["AlgoInstance"] });

                //DataRelation orderTrios_orders_relation = ds.Relations.Add("OrderTrios_Orders", new DataColumn[] { ds.Tables[3].Columns["MainOrderId"] },
                //    new DataColumn[] { ds.Tables[2].Columns["OrderId"] });
                PriceActionInput algoInput = null;
                foreach (DataRow drAlgo in dtActiveAlgos.Rows)
                {
                    //ActiveAlgosView algosView = new ActiveAlgosView();

                    algoInput = new PriceActionInput
                    {
                        AID = Convert.ToInt32(drAlgo["Id"]),
                        Expiry = Convert.ToDateTime(drAlgo["Expiry"]),
                        CTF = Convert.ToInt32(drAlgo["CandleTimeFrame_Mins"]),
                        Qty = Convert.ToInt32(drAlgo["InitialQtyInLotSize"]),
                        BToken = Convert.ToUInt32(drAlgo["BToken"]),
                        UID = Convert.ToString(DBNull.Value != drAlgo["Arg9"] ? drAlgo["Arg9"] : 0),
                        //PS = Convert.ToBoolean(DBNull.Value != drAlgo["PositionSizing"] ? drAlgo["PositionSizing"] : false),
                        //TP = Convert.ToDecimal(DBNull.Value != drAlgo["Arg1"] ? drAlgo["Arg1"] : 0),
                        //SL = Convert.ToDecimal(DBNull.Value != drAlgo["Arg2"] ? drAlgo["Arg2"] : 0),
                        //IntD = Convert.ToDecimal(DBNull.Value != drAlgo["Arg3"] ? drAlgo["Arg3"] : 0) == 1,
                        PnL = Convert.ToDecimal(DBNull.Value != drAlgo["NetPrice"] ? drAlgo["NetPrice"] : 0),


                    };

                    //Arg
                    //arg1: Convert.ToInt32(breakout), upperLimit: _currentPriceRange.Upper, lowerLimit: _currentPriceRange.Lower
                    //arg2: _currentPriceRange.NextUpper, arg3: _currentPriceRange.NextLower
                    //arg4: _previousPriceRange.Upper, arg5: _previousPriceRange.Lower
                    //arg6: _previousPriceRange.NextUpper, arg7: _currentPriceRange.NextLower

                    algoInput.CPR = new PriceRange();
                    algoInput.CPR.Upper = Convert.ToDecimal(DBNull.Value != drAlgo["UpperLimit"] ? drAlgo["UpperLimit"] : 0);
                    algoInput.CPR.Lower = Convert.ToDecimal(DBNull.Value != drAlgo["LowerLimit"] ? drAlgo["LowerLimit"] : 0);
                    algoInput.CPR.NextUpper = Convert.ToDecimal(DBNull.Value != drAlgo["Arg2"] ? drAlgo["Arg2"] : 0);
                    algoInput.CPR.NextLower = Convert.ToDecimal(DBNull.Value != drAlgo["Arg3"] ? drAlgo["Arg3"] : 0);
                    algoInput.PPR = new PriceRange();
                    algoInput.PPR.Upper = Convert.ToDecimal(DBNull.Value != drAlgo["Arg4"] ? drAlgo["Arg4"] : 0);
                    algoInput.PPR.Lower = Convert.ToDecimal(DBNull.Value != drAlgo["Arg5"] ? drAlgo["Arg5"] : 0);
                    algoInput.PPR.NextUpper = Convert.ToDecimal(DBNull.Value != drAlgo["Arg3"] ? drAlgo["Arg3"] : 0);
                    algoInput.PPR.NextLower = Convert.ToDecimal(DBNull.Value != drAlgo["Arg6"] ? drAlgo["Arg6"] : 0);

                    //algoInput.BM = (DBNull.Value != drAlgo["Arg4"] ? drAlgo["Arg5"] : 0) as Breakout;
                    if (DBNull.Value != drAlgo["Arg1"])
                    {
                        switch (Convert.ToDecimal(drAlgo["Arg1"]))
                        {
                            case 0:
                                algoInput.BM = Breakout.NONE;
                                break;
                            case 1:
                                algoInput.BM = Breakout.UP;
                                break;
                            case -1:
                                algoInput.BM = Breakout.DOWN;
                                break;
                        }
                    }


                    //algosView.aid = Convert.ToInt32(drAlgo["AlgoId"]);
                    //algosView.an = Convert.ToString((AlgoIndex)algosView.aid);
                    //algosView.ains = Convert.ToInt32(drAlgo["Id"]);

                    //algosView.expiry = Convert.ToDateTime(drAlgo["Expiry"]).ToString("yyyy-MM-dd");
                    //algosView.mins = Convert.ToInt32(drAlgo["CandleTimeFrame_Mins"]);
                    //algosView.lotsize = Convert.ToInt32(drAlgo["InitialQtyInLotSize"]);
                    //algosView.binstrument = Convert.ToString(drAlgo["BToken"]);
                    //algosView.algodate = Convert.ToDateTime(drAlgo["Timestamp"]).ToString("yyyy-MM-dd");

                    DataRow[] drOrderTrios = drAlgo.GetChildRows(algo_orderTrios_relation);

                    algoInput.ActiveOrderTrios ??= new List<OrderTrio>();
                    List<OrderTrio> orderTrios = new List<OrderTrio>();
                    foreach (DataRow drOrderTrio in drOrderTrios)
                    {
                        if (Convert.ToBoolean(drOrderTrio["Active"]))
                        {
                            OrderTrio orderTrio = new OrderTrio()
                            {
                                Id = Convert.ToInt32(drOrderTrio["Id"]),
                                TargetProfit = Convert.ToDecimal(drOrderTrio["TargetProfit"]),
                                StopLoss = Convert.ToDecimal(drOrderTrio["StopLoss"]),
                                BaseInstrumentStopLoss = Convert.ToDecimal(drOrderTrio["BaseInstrumentStopLoss"])
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
                                    // algosView.Orders.Add(ViewUtility.GetOrderView(o));
                                }
                            }
                            //Instrument option = dl.GetInstrument(orderTrio.Order.Tradingsymbol);
                            //orderTrio.Option = option;
                            orderTrios.Add(orderTrio);
                        }
                    }
                    //activeAlgos.Add(algosView);
                    algoInput.ActiveOrderTrios = orderTrios;

                    //Trade(algoInput, algosView.ains);

                    ////if (algosView.Orders != null)
                    ////{
                    //activeAlgos.Add(algosView);
                    ////}
                }
                // return activeAlgos.ToArray();
                return algoInput;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private async Task<RangeBreakoutCandle> ExecuteAlgo(PriceActionInput paInputs, int algoInstance)
        {
            RangeBreakoutCandle paTrader =
                new RangeBreakoutCandle(TimeSpan.FromMinutes(paInputs.CTF), paInputs.BToken, paInputs.Expiry, paInputs.Qty, paInputs.UID,
                paInputs.TP, paInputs.SL, positionSizing: false, maxLossPerTrade: 0, httpClientFactory: _httpClientFactory, algoInstance: algoInstance);// firebaseMessaging);

            return paTrader;
        }

        private void SendNotification(string title, string body)
        {
            //#if market
            try
            {

                dynamic data = new
                {
                    to = "/topics/NSEIndexAlerts", //registrationToken, // Uncoment this if you want to test for single device
                                                   // registration_ids = singlebatch, // this is for multiple user 
                                                   //topic = "all",

                    notification = new
                    {
                        title = title,     // Notification title
                        body = body    // Notification body data
                    }
                };

                //var data = new
                //{
                //    to = registrationToken,
                //    data = new
                //    {
                //        message = "this is cool",
                //        name = "Prashant",
                //        userId = "1",
                //        status = true
                //    },
                //    notification = new
                //    {
                //        title = "Test App",     // Notification title
                //        body = "test app"    // Notification body data
                //    }
                //};


                //var serializer = new JsonSerializer();// System.Web.Script.Serialization.JavaScriptSerializer();
                //var json = serializer.Serialize(data);

                var json = JsonConvert.SerializeObject(data);


                Byte[] byteArray = System.Text.Encoding.UTF8.GetBytes(json);



                WebRequest tRequest;
                tRequest = WebRequest.Create("https://fcm.googleapis.com/fcm/send");
                tRequest.Method = "post";
                tRequest.ContentType = "application/json";
                tRequest.Headers.Add(string.Format("Authorization: key={0}", SERVER_API_KEY));

                tRequest.Headers.Add(string.Format("Sender: id={0}", SENDER_ID));

                tRequest.ContentLength = byteArray.Length;
                Stream dataStream = tRequest.GetRequestStream();
                dataStream.Write(byteArray, 0, byteArray.Length);
                dataStream.Close();

                WebResponse tResponse = tRequest.GetResponse();

                dataStream = tResponse.GetResponseStream();

                StreamReader tReader = new StreamReader(dataStream);

                String sResponseFromServer = tReader.ReadToEnd();
                Thread.Sleep(200);
                tReader.Close();
                dataStream.Close();
                tResponse.Close();
                Thread.Sleep(1000);
            }
            catch (Exception)
            {
                throw;
            }
            //#endif
        }

        private void BC_OnTradeExit(Order st)
        {
            //publish trade details and count
            //Bind with trade token details, use that as an argument
            //OrderCore.PublishOrder(st);
            //Thread.Sleep(100);
        }

        private void BC_OnTradeEntry(Order st)
        {
            //publish trade details and count
            //OrderCore.PublishOrder(st);
            //Thread.Sleep(100);
        }

        private async Task NMQClientSubscription(RangeBreakoutCandle paTrader, uint token)
        {
            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(new List<uint>() { token });

            await zmqClient.Subscribe(paTrader);
        }
#if local
        //private async Task KFKClientSubscription(RangeBreakoutCandle paTrader, uint token)
        //{
        //    kfkClient = new KSubscriber();
        //    kfkClient.AddSubscriber(new List<uint>() { token });

        //    await kfkClient.Subscribe(paTrader);
        //}
        private async Task ObserverSubscription(RangeBreakoutCandle paTrader, uint token)
        {
            GlobalObjects.ObservableFactory ??= new ObservableFactory();
            paTrader.Subscribe(GlobalObjects.ObservableFactory);
        }
        //private async Task MQTTCientSubscription(RangeBreakoutCandle paTrader, uint token)
        //{
        //    mqttSubscriber = new MQTTSubscriber();
        //    mqttSubscriber.
        //    paTrader.Subscribe(GlobalObjects.ObservableFactory);
        //}
#endif
        private void PATrade_OnOptionUniverseChange(RangeBreakoutCandle source)
        {
            try
            {
                zmqClient.AddSubscriber(source.SubscriptionTokens);
#if local
                // kfkClient.AddSubscriber(source.SubscriptionTokens);
#endif
            }
            catch (Exception ex)
            {
                throw ex;

            }
        }
        [HttpGet("healthy")]
        public Task<int> Health()
        {
            return Task.FromResult((int)AlgoIndex.BC);
        }

        [HttpPut("{ain}")]
        public bool Put(int ain, [FromBody] int start)
        {
            List<RangeBreakoutCandle> activeAlgoObjects;
            if (!_cache.TryGetValue(key, out activeAlgoObjects))
            {
                activeAlgoObjects = new List<RangeBreakoutCandle>();
            }

            RangeBreakoutCandle algoObject = activeAlgoObjects.FirstOrDefault(x => x.AlgoInstance == ain);
            if (algoObject != null)
            {
                algoObject.StopTrade(!Convert.ToBoolean(start));
            }
            _cache.Set(key, activeAlgoObjects);

            return true;
        }
    }
}
