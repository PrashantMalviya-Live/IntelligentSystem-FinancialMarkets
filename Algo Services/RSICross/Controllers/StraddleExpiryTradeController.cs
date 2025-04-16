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
//using LocalDBData;

namespace RSICross.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StraddleExpiryTradeController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private const string key = "ACTIVE_StraddleExpiryTrade_OBJECT";
        private const string SERVER_API_KEY = @"AAAA0HFQ8ag:APA91bFyK2buMq1eStQ2vh4ax83NkB-NVkax04BvlEX7G9iYTaJIGyayq8MLcPHQHDjXc1YqB5BxMnSghpToKtbmMRARJLFJuamLm6-7qgqFD91moXQLz_xJOb0dVR_Chbt8y8tkNTqo";
        private const string SENDER_ID = "895254327720";
        private const string registrationToken = "dhovn1Sk88c:APA91bFuHzKpxeKbfn-5oQG4xZ54uKTzGAzj7vqGs61RwYm_D1irujGL1U64npy2O1Yt8cKGaS11q7KXu9HGltbnSrDA4roCNRvUDSdTAy0DhabU2Br0aQTkrOAc2Z8j6cqSmv7rECEu";

        IConfiguration configuration;
        ZMQClient zmqClient;
        //KSubscriber kfkClient;
        //Publisher mqttPublisher;
        //MQTTSubscriber mqttSubscriber;
        private IMemoryCache _cache;
        public StraddleExpiryTradeController(IMemoryCache cache, IHttpClientFactory httpClientFactory)
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
            try
            {
                string userId = "PM27031981";
                DataLogic dl = new DataLogic();
                DataSet ds = dl.GetActiveAlgos(AlgoIndex.BC, userId);

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

                    StraddleInput algoInput = new StraddleInput
                    {
                        Expiry = Convert.ToDateTime(drAlgo["Expiry"]),
                        CTF = Convert.ToInt32(drAlgo["CandleTimeFrame_Mins"]),
                        Qty = Convert.ToInt32(drAlgo["InitialQtyInLotSize"]),
                        BToken = Convert.ToUInt32(drAlgo["BToken"]),
                        UID = Convert.ToString(DBNull.Value != drAlgo["Arg9"] ? drAlgo["Arg9"] : 0),
                        //PS = Convert.ToBoolean(DBNull.Value != drAlgo["PositionSizing"] ? drAlgo["PositionSizing"] : false),
                        TP = Convert.ToDecimal(DBNull.Value != drAlgo["Arg1"] ? drAlgo["Arg1"] : 0),
                        SL = Convert.ToDecimal(DBNull.Value != drAlgo["Arg2"] ? drAlgo["Arg2"] : 0),
                        IntraDay = Convert.ToDecimal(DBNull.Value != drAlgo["Arg3"] ? drAlgo["Arg3"] : 0) == 1,
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
                                StopLoss = Convert.ToDecimal(drOrderTrio["StopLoss"])
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
        public async Task<ActiveAlgosView> Trade([FromBody] StraddleInput paInputs, int algoInstance = 0)
        {
            StraddleExpiryTrade paTrader = await ExecuteAlgo(paInputs, algoInstance);

            //#if local
            //            DataLogic dl = new DataLogic();
            //            DataSet dsPAInputs = dl.LoadAlgoInputs(AlgoIndex.StraddleExpiryTrade, Convert.ToDateTime("2021-12-01"), Convert.ToDateTime("2021-12-30"));

            //            List<StraddleInput> priceActionInputs = new List<StraddleInput>();

            //            for (int i = 0; i < dsPAInputs.Tables[0].Rows.Count; i++)
            //            {
            //                DataRow drPAInputs = dsPAInputs.Tables[0].Rows[i];

            //                priceActionInputs.Add(new StraddleInput()
            //                {
            //                    BToken = (uint)drPAInputs["BToken"],
            //                    CTF = (int)drPAInputs["CTF"],
            //                    Expiry = (DateTime)drPAInputs["Expiry"],
            //                    PD_H = (decimal)drPAInputs["PD_H"],
            //                    PD_L = (decimal)drPAInputs["PD_L"],
            //                    PD_C = (decimal)drPAInputs["PD_C"],
            //                    SL = (decimal)drPAInputs["SL"],
            //                    TP = (decimal)drPAInputs["TP"],
            //                    Qty = (int)drPAInputs["QTY"],
            //                }) ;
            //            }
            //            foreach (var priceActionInput in priceActionInputs)
            //            {
            //                paTrader = ExecuteAlgo(paInputs);
            //            }

            //#elif market
            //            paTrader = ExecuteAlgo(paInputs);
            //#endif

            paTrader.OnOptionUniverseChange += PATrade_OnOptionUniverseChange;
            paTrader.OnCriticalEvents += SendNotification;// (string title, string body)
            paTrader.OnTradeEntry += BC_OnTradeEntry;
            paTrader.OnTradeExit += BC_OnTradeExit;

            List<StraddleExpiryTrade> activeAlgoObjects = _cache.Get<List<StraddleExpiryTrade>>(key);

            if (activeAlgoObjects == null)
            {
                activeAlgoObjects = new List<StraddleExpiryTrade>();
            }
            activeAlgoObjects.Add(paTrader);
            _cache.Set(key, activeAlgoObjects);

            Task task = Task.Run(() => NMQClientSubscription(paTrader, paInputs.BToken));
#if local
            //Task observerSubscriptionTask = Task.Run(() => ObserverSubscription(paTrader, paInputs.BToken));
            //Task kftask = Task.Run(() => KFKClientSubscription(paTrader, paInputs.BToken));
#endif

            //await task;
            return new ActiveAlgosView
            {
                aid = Convert.ToInt32(AlgoIndex.EST),
                an = Convert.ToString((AlgoIndex)AlgoIndex.EST),
                ains = paTrader.AlgoInstance,
                algodate = DateTime.Now.ToString("yyyy-MM-dd"),
                binstrument = paInputs.BToken.ToString(),
                expiry = paInputs.Expiry.ToString("yyyy-MM-dd"),
                lotsize = paInputs.Qty,
                mins = paInputs.CTF
            };
        }

        private async Task<StraddleExpiryTrade> ExecuteAlgo(StraddleInput paInputs, int algoInstance)
        {
            //SendNotification("title","body");
            //FirebaseApp fbp = FirebaseApp.Create(new AppOptions()
            //{
            //    ProjectId = "marketalerts-e9c4e",
            //    Credential = GoogleCredential.FromFile("privatekey.json")
            //});
            ////FirebaseApp.Create(new AppOptions()
            ////{
            ////    Credential = GoogleCredential.GetApplicationDefault(),
            ////});

            //Console.WriteLine(fbp.Name);
            //var registrationToken = "dfKA3WVeLws:APA91bG4mUWLneeduvP99UD6gRweF8yy-9aYDUT-cGuhr00qCdq8uMOezIHi_dW3S9Pn2rsxvIhigkB9DoEFY9UFXoyzzHjBiPgVFfYW4uDXWcCtQz-R-PeEFF-Kl2rXm_n3Cy6pmo8u";

            //FirebaseMessaging firebaseMessaging = FirebaseMessaging.DefaultInstance;
            //var fcm = FirebaseMessaging.GetMessaging(fbp);
            //var message = new Message()
            //{
            //    Data = new Dictionary<string, string>() { { "Niftay", "1760a0" }, },
            //    //Topic = Constants.MARKET_ALERTS,
            //    Token = registrationToken,
            //    Notification = new Notification() { Title = "Index Aalerts", Body = "Staarted" }
            //};
            //try
            //{
            //    //var result = await fcm.SendAsync(message).ConfigureAwait(false);
            //    Console.WriteLine(firebaseMessaging.SendAsync(message).Result);
            //}
            //catch (FirebaseMessagingException ex)
            //{

            //}
            //catch (Exception ex)
            //{

            //}

            //OHLC pdOHLC = new OHLC();
            //pdOHLC.High = paInputs.PD_H;
            //pdOHLC.Low = paInputs.PD_L;
            //pdOHLC.Close = paInputs.PD_C;

            //OHLC pwOHLC = new OHLC();
            //pwOHLC.High = paInputs.PW_H;
            //pwOHLC.Low = paInputs.PW_L;
            //pwOHLC.Close = paInputs.PW_C;

            //paInputs.BToken = Convert.ToUInt32(Constants.MIDCPNIFTY_TOKEN);
            DataLogic dl = new DataLogic();
            paInputs.CTF = 15;
            //paInputs.Expiry = dl.GetCurrentWeeklyExpiry(DateTime.Today, paInputs.BToken); //Convert.ToDateTime("2023-08-17");
            //paInputs.Expiry = Convert.ToDateTime("2023-08-15");
            //paInputs.BToken = 257801;
            StraddleExpiryTrade paTrader =
                new StraddleExpiryTrade(TimeSpan.FromMinutes(paInputs.CTF), paInputs.BToken, paInputs.Expiry, paInputs.Qty, paInputs.UID,
                paInputs.TP, paInputs.SL, paInputs.IntraDay, paInputs.SPI, paInputs.Flag, paInputs.TR, positionSizing: false, 
                maxLossPerTrade: 0, httpClientFactory: _httpClientFactory, algoInstance: algoInstance);// firebaseMessaging);

            return paTrader;
        }

        public void SendNotification(string title, string body)
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
            OrderCore.PublishOrder(st);
            Thread.Sleep(100);
        }

        private void BC_OnTradeEntry(Order st)
        {
            //publish trade details and count
            OrderCore.PublishOrder(st);
            Thread.Sleep(100);
        }

        private async Task NMQClientSubscription(StraddleExpiryTrade paTrader, uint token)
        {
            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(new List<uint>() { token });

            await zmqClient.Subscribe(paTrader);
        }
#if local
        //private async Task KFKClientSubscription(StraddleExpiryTrade paTrader, uint token)
        //{
        //    kfkClient = new KSubscriber();
        //    kfkClient.AddSubscriber(new List<uint>() { token });

        //    await kfkClient.Subscribe(paTrader);
        //}
        //private async Task ObserverSubscription(StraddleExpiryTrade paTrader, uint token)
        //{
        //    GlobalObjects.ObservableFactory ??= new ObservableFactory();
        //    paTrader.Subscribe(GlobalObjects.ObservableFactory);
        //}
        //private async Task MQTTCientSubscription(StraddleExpiryTrade paTrader, uint token)
        //{
        //    mqttSubscriber = new MQTTSubscriber();
        //    mqttSubscriber.
        //    paTrader.Subscribe(GlobalObjects.ObservableFactory);
        //}
#endif
        private void PATrade_OnOptionUniverseChange(StraddleExpiryTrade source)
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
            return Task.FromResult((int)AlgoIndex.EST);
        }

        [HttpPut("{ain}")]
        public bool Put(int ain, [FromBody] int start)
        {
            List<StraddleExpiryTrade> activeAlgoObjects;
            if (!_cache.TryGetValue(key, out activeAlgoObjects))
            {
                activeAlgoObjects = new List<StraddleExpiryTrade>();
            }

            StraddleExpiryTrade algoObject = activeAlgoObjects.FirstOrDefault(x => x.AlgoInstance == ain);
            if (algoObject != null)
            {
                algoObject.StopTrade(!Convert.ToBoolean(start));
            }
            _cache.Set(key, activeAlgoObjects);

            return true;
        }
    }
}
