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
using System.Threading.Channels;
//using LocalDBData;

namespace CryptoAlgos.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ActiveStrangleBuyController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private const string key = "ACTIVE_C_STR_BUY_OBJECT";
        private const string SERVER_API_KEY = @"AAAA0HFQ8ag:APA91bFyK2buMq1eStQ2vh4ax83NkB-NVkax04BvlEX7G9iYTaJIGyayq8MLcPHQHDjXc1YqB5BxMnSghpToKtbmMRARJLFJuamLm6-7qgqFD91moXQLz_xJOb0dVR_Chbt8y8tkNTqo";
        private const string SENDER_ID = "895254327720";
        private const string registrationToken = "dhovn1Sk88c:APA91bFuHzKpxeKbfn-5oQG4xZ54uKTzGAzj7vqGs61RwYm_D1irujGL1U64npy2O1Yt8cKGaS11q7KXu9HGltbnSrDA4roCNRvUDSdTAy0DhabU2Br0aQTkrOAc2Z8j6cqSmv7rECEu";

        IConfiguration configuration;
        CryptoZMQClient zmqClient;
        //KSubscriber kfkClient;
        //Publisher mqttPublisher;
        //MQTTSubscriber mqttSubscriber;
        private IMemoryCache _cache;
        public ActiveStrangleBuyController(IMemoryCache cache, IHttpClientFactory httpClientFactory)
        {
            this._cache = cache;
            this._httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public IEnumerable<BInstumentView> Get()
        {
            DataLogic dl = new DataLogic();
            //List<Instrument> bInstruments = dl.RetrieveBaseInstruments();

            List<BInstumentView> bInstruments = new List<BInstumentView>() { new BInstumentView() { InstrumentToken = 27, TradingSymbol = "BTCUSD" } };
            return bInstruments.ToList();// (from n in bInstruments select new BInstumentView { InstrumentToken = n.InstrumentToken, TradingSymbol = n.TradingSymbol.Trim(' ') }).ToList();
        }

        [HttpGet("{token}")]
        public IEnumerable<string> OptionExpiries(uint token)
        {
            DataLogic dl = new DataLogic();
            //List<string> expiryList = dl.RetrieveOptionExpiries(token);

            List<string> expiryList = new List<string>();
            expiryList.Add("02/03/2025");
            expiryList.Add("03/03/2025");
            expiryList.Add("04/03/2025");

            return expiryList;
        }

        [HttpPost]
        public async Task<ActiveAlgosView> Trade([FromBody] OptionTradeInputs paInputs, int algoInstance = 0)
        {
            ActiveBuyStrangleWithVariableQty paTrader = await ExecuteAlgo(paInputs);

            paTrader.OnOptionUniverseChange += PATrade_OnOptionUniverseChange;
            paTrader.OnTradeEntry += OptionSellwithRSI_OnTradeEntry;
            paTrader.OnTradeExit += OptionSellwithRSI_OnTradeExit;

            List<ActiveBuyStrangleWithVariableQty> activeAlgoObjects = _cache.Get<List<ActiveBuyStrangleWithVariableQty>>(key);

            if (activeAlgoObjects == null)
            {
                activeAlgoObjects = new List<ActiveBuyStrangleWithVariableQty>();
            }
            activeAlgoObjects.Add(paTrader);
            _cache.Set(key, activeAlgoObjects);

            string channel = "l1_orderbook";
            Task task = Task.Run(() => NMQClientSubscription(paTrader, channel));
#if local
            //Task observerSubscriptionTask = Task.Run(() => ObserverSubscription(paTrader, paInputs.BToken));
            //Task kftask = Task.Run(() => KFKClientSubscription(paTrader, paInputs.BToken));
#endif

            //await task;
            return new ActiveAlgosView
            {
                aid = Convert.ToInt32(AlgoIndex.Crypto_ActiveStrangleBuy),
                an = Convert.ToString((AlgoIndex)AlgoIndex.Crypto_ActiveStrangleBuy),
                ains = paTrader.AlgoInstance,
                algodate = DateTime.Now.ToString("yyyy-MM-dd"),
                binstrument = paInputs.BToken.ToString(),
                expiry = paInputs.Expiry.ToString("yyyy-MM-dd"),
                lotsize = paInputs.IQty,
                mins = 0
            };
        }

        private async Task<ActiveBuyStrangleWithVariableQty> ExecuteAlgo(OptionTradeInputs paInputs)
        {
            paInputs.BToken = 27;
            paInputs.Expiry = DateTime.Today;//.AddDays(1);
            paInputs.IQty = 20;
            paInputs.SQty = 10;
            paInputs.MQty = 50;


            paInputs.uid = "PMDEUID";
            paInputs.TP = 10000;
            paInputs.SL = 2000;

            ActiveBuyStrangleWithVariableQty paTrader =
              new ActiveBuyStrangleWithVariableQty(paInputs.BToken, paInputs.Expiry, paInputs.IQty, 
              paInputs.SQty, paInputs.MQty, paInputs.TP,paInputs.SL, 0.1M, 1M, 0.05M, 
              algoIndex: AlgoIndex.Crypto_ActiveStrangleBuy, algoInstance: 0, httpClientFactory: _httpClientFactory);

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

        private async Task NMQClientSubscription(ActiveBuyStrangleWithVariableQty paTrader, string channel)
        {
            zmqClient = new CryptoZMQClient();
            zmqClient.AddSubscriber(new List<string>() { channel });

            await zmqClient.Subscribe(paTrader);
        }
#if local
        //private async Task KFKClientSubscription(PriceDirectionalFutureOptions paTrader, uint token)
        //{
        //    kfkClient = new KSubscriber();
        //    kfkClient.AddSubscriber(new List<uint>() { token });

        //    await kfkClient.Subscribe(paTrader);
        //}
        private async Task ObserverSubscription(PriceDirectionalFutureOptions paTrader, uint token)
        {
            GlobalObjects.ObservableFactory ??= new ObservableFactory();
            paTrader.Subscribe(GlobalObjects.ObservableFactory);
        }
        //private async Task MQTTCientSubscription(PriceDirectionalFutureOptions paTrader, uint token)
        //{
        //    mqttSubscriber = new MQTTSubscriber();
        //    mqttSubscriber.
        //    paTrader.Subscribe(GlobalObjects.ObservableFactory);
        //}
#endif
        private async void PATrade_OnOptionUniverseChange(ActiveBuyStrangleWithVariableQty source, string channel)
        {
            try
            {
                zmqClient = new CryptoZMQClient();
                zmqClient.AddSubscriber(new List<string>() { channel });

                zmqClient.Subscribe(source).Wait();
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
            return Task.FromResult((int)AlgoIndex.Crypto_ActiveStrangleBuy);
        }

        [HttpPut("{ain}")]
        public bool Put(int ain, [FromBody] int start)
        {
            List<ActiveBuyStrangleWithVariableQty> activeAlgoObjects;
            if (!_cache.TryGetValue(key, out activeAlgoObjects))
            {
                activeAlgoObjects = new List<ActiveBuyStrangleWithVariableQty>();
            }

            ActiveBuyStrangleWithVariableQty algoObject = activeAlgoObjects.FirstOrDefault(x => x.AlgoInstance == ain);
            if (algoObject != null)
            {
                algoObject.StopTrade(!Convert.ToBoolean(start));
            }
            _cache.Set(key, activeAlgoObjects);

            return true;
        }
    }
}
