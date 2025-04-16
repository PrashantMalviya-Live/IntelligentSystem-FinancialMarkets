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

namespace TradeEMACross.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TJ3Controller : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private const string key = "ACTIVE_TJ3_OBJECT";
        private const string SERVER_API_KEY = @"AAAA0HFQ8ag:APA91bFyK2buMq1eStQ2vh4ax83NkB-NVkax04BvlEX7G9iYTaJIGyayq8MLcPHQHDjXc1YqB5BxMnSghpToKtbmMRARJLFJuamLm6-7qgqFD91moXQLz_xJOb0dVR_Chbt8y8tkNTqo";
        private const string SENDER_ID = "895254327720";
        private const string registrationToken = "dhovn1Sk88c:APA91bFuHzKpxeKbfn-5oQG4xZ54uKTzGAzj7vqGs61RwYm_D1irujGL1U64npy2O1Yt8cKGaS11q7KXu9HGltbnSrDA4roCNRvUDSdTAy0DhabU2Br0aQTkrOAc2Z8j6cqSmv7rECEu";

        IConfiguration configuration;
        ZMQClient zmqClient;
        //KSubscriber kfkClient;
        //Publisher mqttPublisher;
        //MQTTSubscriber mqttSubscriber;
        private IMemoryCache _cache;
        public TJ3Controller(IMemoryCache cache, IHttpClientFactory httpClientFactory)
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

        [HttpPost]
        public async Task<ActiveAlgosView> Trade([FromBody] PriceActionInput paInputs, int algoInstance = 0)
        {
            TJ2 paTrader = await ExecuteAlgo(paInputs);
            //#if local
            //            DataLogic dl = new DataLogic();
            //            DataSet dsPAInputs = dl.LoadAlgoInputs(AlgoIndex.TJ2, Convert.ToDateTime("2021-12-01"), Convert.ToDateTime("2021-12-30"));

            //            List<PriceActionInput> priceActionInputs = new List<PriceActionInput>();

            //            for (int i = 0; i < dsPAInputs.Tables[0].Rows.Count; i++)
            //            {
            //                DataRow drPAInputs = dsPAInputs.Tables[0].Rows[i];

            //                priceActionInputs.Add(new PriceActionInput()
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
            paTrader.OnTradeEntry += OptionSellwithRSI_OnTradeEntry;
            paTrader.OnTradeExit += OptionSellwithRSI_OnTradeExit;

            List<TJ2> activeAlgoObjects = _cache.Get<List<TJ2>>(key);

            if (activeAlgoObjects == null)
            {
                activeAlgoObjects = new List<TJ2>();
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
                aid = Convert.ToInt32(AlgoIndex.TJ2),
                an = Convert.ToString((AlgoIndex)AlgoIndex.TJ2),
                ains = paTrader.AlgoInstance,
                algodate = DateTime.Now.ToString("yyyy-MM-dd"),
                binstrument = paInputs.BToken.ToString(),
                expiry = paInputs.Expiry.ToString("yyyy-MM-dd"),
                lotsize = paInputs.Qty,
                mins = paInputs.CTF
            };
        }

        private async Task<TJ2> ExecuteAlgo(PriceActionInput paInputs)
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

            //paInputs.RToken = Constants.BANK_NIFTY_TOKEN;
            //TJ2 paTrader =
            //    new TJ2(TimeSpan.FromMinutes(paInputs.CTF), paInputs.BToken, paInputs.RToken, paInputs.Expiry, paInputs.Qty, paInputs.UID, paInputs.TP, paInputs.SL, paInputs.IntD, //pdOHLC, pwOHLC,
            //                                                                                                                                                 //paInputs.PD_BH, paInputs.PD_BL, paInputs.PS_H, paInputs.PS_L, paInputs.PW_L, paInputs.PW_H, 
            //    positionSizing: false, maxLossPerTrade: 0, httpClientFactory: _httpClientFactory);// firebaseMessaging);

            paInputs.CTF = 5;
            paInputs.BToken = 260105;
            //paInputs.Expiry = Convert.ToDateTime("2023-05-11");
            paInputs.Qty = 6;
            paInputs.UID = "PM27031981";

            TJ2 paTrader =
              new TJ2(TimeSpan.FromMinutes(paInputs.CTF), paInputs.BToken, paInputs.Expiry, paInputs.Qty, paInputs.UID,
              paInputs.TP, paInputs.SL, paInputs.IntD, paInputs.PnL,
              // paInputs.TP1, paInputs.TP2, paInputs.TP3, paInputs.TP4, paInputs.SL1, paInputs.SL2, paInputs.SL3, paInputs.SL4,
              positionSizing: false, maxLossPerTrade: 0, httpClientFactory: _httpClientFactory, algoInstance: 0);// firebaseMessaging);

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

        private async Task NMQClientSubscription(TJ2 paTrader, uint token)
        {
            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(new List<uint>() { token });

            await zmqClient.Subscribe(paTrader);
        }
#if local
        //private async Task KFKClientSubscription(TJ2 paTrader, uint token)
        //{
        //    kfkClient = new KSubscriber();
        //    kfkClient.AddSubscriber(new List<uint>() { token });

        //    await kfkClient.Subscribe(paTrader);
        //}
        private async Task ObserverSubscription(TJ2 paTrader, uint token)
        {
            GlobalObjects.ObservableFactory ??= new ObservableFactory();
            paTrader.Subscribe(GlobalObjects.ObservableFactory);
        }
        //private async Task MQTTCientSubscription(TJ2 paTrader, uint token)
        //{
        //    mqttSubscriber = new MQTTSubscriber();
        //    mqttSubscriber.
        //    paTrader.Subscribe(GlobalObjects.ObservableFactory);
        //}
#endif
        private void PATrade_OnOptionUniverseChange(TJ2 source)
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
            return Task.FromResult((int)AlgoIndex.TJ2);
        }

        [HttpPut("{ain}")]
        public bool Put(int ain, [FromBody] int start)
        {
            List<TJ2> activeAlgoObjects;
            if (!_cache.TryGetValue(key, out activeAlgoObjects))
            {
                activeAlgoObjects = new List<TJ2>();
            }

            TJ2 algoObject = activeAlgoObjects.FirstOrDefault(x => x.AlgoInstance == ain);
            if (algoObject != null)
            {
                algoObject.StopTrade(!Convert.ToBoolean(start));
            }
            _cache.Set(key, activeAlgoObjects);

            return true;
        }
    }
}
