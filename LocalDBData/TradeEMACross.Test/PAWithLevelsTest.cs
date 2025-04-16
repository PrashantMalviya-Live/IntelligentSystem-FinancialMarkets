using Algorithms.Algorithms;
using Algos.Utilities.Views;
using GlobalLayer;
using System;

namespace LocalDBData.Test
{
    internal class PAWithLevelsTest : ITest
    {
        PAWithLevels paTrader;
        //private readonly IHttpClientFactory _httpClientFactory;
        //private const string key = "ACTIVE_PA_OBJECT";
        //private const string SERVER_API_KEY = @"AAAA0HFQ8ag:APA91bFyK2buMq1eStQ2vh4ax83NkB-NVkax04BvlEX7G9iYTaJIGyayq8MLcPHQHDjXc1YqB5BxMnSghpToKtbmMRARJLFJuamLm6-7qgqFD91moXQLz_xJOb0dVR_Chbt8y8tkNTqo";
        //private const string SENDER_ID = "895254327720";
        //private const string registrationToken = "dhovn1Sk88c:APA91bFuHzKpxeKbfn-5oQG4xZ54uKTzGAzj7vqGs61RwYm_D1irujGL1U64npy2O1Yt8cKGaS11q7KXu9HGltbnSrDA4roCNRvUDSdTAy0DhabU2Br0aQTkrOAc2Z8j6cqSmv7rECEu";

        //IConfiguration configuration;
        //ZMQClient zmqClient;
        ////KSubscriber kfkClient;
        ////Publisher mqttPublisher;
        ////MQTTSubscriber mqttSubscriber;
        //private IMemoryCache _cache;
        //public PriceActionController(IMemoryCache cache, IHttpClientFactory httpClientFactory)
        //{
        //    this._cache = cache;
        //    this._httpClientFactory = httpClientFactory;
        //}

        //[HttpGet]
        //public IEnumerable<BInstumentView> Get()
        //{
        //    DataLogic dl = new DataLogic();
        //    List<Instrument> bInstruments = dl.RetrieveBaseInstruments();

        //    return (from n in bInstruments select new BInstumentView { InstrumentToken = n.InstrumentToken, TradingSymbol = n.TradingSymbol.Trim(' ') }).ToList();
        //}

        //[HttpGet("{token}")]
        //public IEnumerable<string> OptionExpiries(uint token)
        //{
        //    DataLogic dl = new DataLogic();
        //    List<string> expiryList = dl.RetrieveOptionExpiries(token);
        //    return expiryList;
        //}

        public void Execute(PriceActionInput paInputs)
        {
            paTrader = ExecuteAlgo(paInputs);
            
            paTrader.OnOptionUniverseChange += ObserverSubscription;
            paTrader.OnCriticalEvents += SendNotification;// (string title, string body)
            paTrader.OnTradeEntry += OptionSellwithRSI_OnTradeEntry;
            paTrader.OnTradeExit += OptionSellwithRSI_OnTradeExit;

//            Task task = Task.Run(() => NMQClientSubscription(paTrader, paInputs.BToken));
//#if Local
           // Task observerSubscriptionTask = Task.Run(() => ObserverSubscription(paTrader));
//            Task kftask = Task.Run(() => KFKClientSubscription(paTrader, paInputs.BToken));
//#endif
        }

        public void OnNext(Tick tick)
        {
            paTrader.OnNext(tick);
        }
        public void StopTrade(bool stopTrade)
        {
            paTrader.StopTrade(stopTrade);
        }
        private void SendNotification(string title, string body)
        {

        }
        private PAWithLevels ExecuteAlgo(PriceActionInput paInputs)
        {
            PAWithLevels paTrader =
                new PAWithLevels(TimeSpan.FromMinutes(paInputs.CTF), paInputs.BToken, paInputs.Expiry, paInputs.Qty, paInputs.UID, paInputs.TP, paInputs.SL, //pdOHLC, pwOHLC,
                                                                                                                                               //paInputs.PD_BH, paInputs.PD_BL, paInputs.PS_H, paInputs.PS_L, paInputs.PW_L, paInputs.PW_H, 
                positionSizing: false, maxLossPerTrade: 0, httpClientFactory: null);// firebaseMessaging);

            return paTrader;
        }

        

        private void OptionSellwithRSI_OnTradeExit(Order st)
        {
            ////publish trade details and count
            ////Bind with trade token details, use that as an argument
            //OrderCore.PublishOrder(st);
            //Thread.Sleep(100);
        }

        private void OptionSellwithRSI_OnTradeEntry(Order st)
        {
            ////publish trade details and count
            //OrderCore.PublishOrder(st);
            //Thread.Sleep(100);
        }

        private void ObserverSubscription(PAWithLevels paTrader)
        {
            //GlobalObjects.ObservableFactory ??= new ObservableFactory();
            //paTrader.Subscribe(GlobalObjects.ObservableFactory);
        }

        //private async Task NMQClientSubscription(PAWithLevels paTrader, uint token)
        //{
        //    zmqClient = new ZMQClient();
        //    zmqClient.AddSubscriber(new List<uint>() { token });

        //    await zmqClient.Subscribe(paTrader);
        //}
#if local
        private async Task KFKClientSubscription(PAWithLevels paTrader, uint token)
        {
            kfkClient = new KSubscriber();
            kfkClient.AddSubscriber(new List<uint>() { token });

            await kfkClient.Subscribe(paTrader);
        }
        //private async Task ObserverSubscription(PAWithLevels paTrader, uint token)
        //{
        //    GlobalObjects.ObservableFactory ??= new ObservableFactory();
        //    paTrader.Subscribe(GlobalObjects.ObservableFactory);
        //}
        //private async Task MQTTCientSubscription(PAWithLevels paTrader, uint token)
        //{
        //    mqttSubscriber = new MQTTSubscriber();
        //    mqttSubscriber.
        //    paTrader.Subscribe(GlobalObjects.ObservableFactory);
        //}
#endif
//        private void PATrade_OnOptionUniverseChange(PAWithLevels source)
//        {
//            try
//            {
//                zmqClient.AddSubscriber(source.SubscriptionTokens);
//#if local
//                        kfkClient.AddSubscriber(source.SubscriptionTokens);
//#endif
//            }
//            catch (Exception ex)
//            {
//                throw ex;

//            }
//        }
        //        [HttpGet("healthy")]
        //        public Task<int> Health()
        //        {
        //            return Task.FromResult((int)AlgoIndex.PAWithLevels);
        //        }

        //        [HttpPut("{ain}")]
        //        public bool Put(int ain, [FromBody] int start)
        //        {
        //            List<PAWithLevels> activeAlgoObjects;
        //            if (!_cache.TryGetValue(key, out activeAlgoObjects))
        //            {
        //                activeAlgoObjects = new List<PAWithLevels>();
        //            }

        //            PAWithLevels algoObject = activeAlgoObjects.FirstOrDefault(x => x.AlgoInstance == ain);
        //            if (algoObject != null)
        //            {
        //                algoObject.StopTrade(!Convert.ToBoolean(start));
        //            }
        //            _cache.Set(key, activeAlgoObjects);

        //            return true;
        //        }
    }
}

