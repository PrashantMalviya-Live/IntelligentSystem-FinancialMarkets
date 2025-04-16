using Algorithms.Algorithms;
using Algos.Utilities.Views;
using GlobalLayer;
using System;

namespace LocalDBData.Test
{
    internal class TJ5Test : ITest
    {
        TJ5 paTrader;
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
        private TJ5 ExecuteAlgo(PriceActionInput paInputs)
        {
            TJ5 paTrader =
                new TJ5(TimeSpan.FromMinutes(paInputs.CTF), paInputs.BToken, paInputs.RToken.Value, paInputs.Expiry,
                paInputs.Qty, paInputs.UID, paInputs.TP, paInputs.SL, //pdOHLC, pwOHLC,
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

        private void ObserverSubscription(TJ5 paTrader)
        {
            //GlobalObjects.ObservableFactory ??= new ObservableFactory();
            //paTrader.Subscribe(GlobalObjects.ObservableFactory);
        }

        //private async Task NMQClientSubscription(TJ5 paTrader, uint token)
        //{
        //    zmqClient = new ZMQClient();
        //    zmqClient.AddSubscriber(new List<uint>() { token });

        //    await zmqClient.Subscribe(paTrader);
        //}
#if local
        private async Task KFKClientSubscription(TJ5 paTrader, uint token)
        {
            kfkClient = new KSubscriber();
            kfkClient.AddSubscriber(new List<uint>() { token });

            await kfkClient.Subscribe(paTrader);
        }
        //private async Task ObserverSubscription(TJ5 paTrader, uint token)
        //{
        //    GlobalObjects.ObservableFactory ??= new ObservableFactory();
        //    paTrader.Subscribe(GlobalObjects.ObservableFactory);
        //}
        //private async Task MQTTCientSubscription(TJ5 paTrader, uint token)
        //{
        //    mqttSubscriber = new MQTTSubscriber();
        //    mqttSubscriber.
        //    paTrader.Subscribe(GlobalObjects.ObservableFactory);
        //}
#endif
        //        private void PATrade_OnOptionUniverseChange(TJ5 source)
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
        //            return Task.FromResult((int)AlgoIndex.TJ5);
        //        }

        //        [HttpPut("{ain}")]
        //        public bool Put(int ain, [FromBody] int start)
        //        {
        //            List<TJ5> activeAlgoObjects;
        //            if (!_cache.TryGetValue(key, out activeAlgoObjects))
        //            {
        //                activeAlgoObjects = new List<TJ5>();
        //            }

        //            TJ5 algoObject = activeAlgoObjects.FirstOrDefault(x => x.AlgoInstance == ain);
        //            if (algoObject != null)
        //            {
        //                algoObject.StopTrade(!Convert.ToBoolean(start));
        //            }
        //            _cache.Set(key, activeAlgoObjects);

        //            return true;
        //        }
    }
}

