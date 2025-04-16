using Algorithms.Algorithms;
using Algos.Utilities.Views;
using GlobalLayer;
using System;

namespace LocalDBData.Test
{
    internal class StopAndReverseTest : ITest
    {
        //FIND AND REPLACE STOPANDREVERSE AND STOPANDREVERSEOPTIONS AND BREAKOUTLIVE BREAKOUTCANDLES
        StopAndReverse paTrader;

        public void Execute(PriceActionInput paInputs)
        {
            paTrader = ExecuteAlgo(paInputs);

            paTrader.OnOptionUniverseChange += ObserverSubscription;
            paTrader.OnCriticalEvents += SendNotification;
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
        private StopAndReverse ExecuteAlgo(PriceActionInput paInputs)
        {
            //StopAndReverse paTrader =
            //    new StopAndReverse(paInputs.BToken, paInputs.Expiry, paInputs.Qty, paInputs.TP, paInputs.SL, 
            //    positionSizing: false, maxLossPerTrade: 0, httpClientFactory: null);// firebaseMessaging);

            StopAndReverse paTrader =
                new StopAndReverse(paInputs.BToken, paInputs.Expiry, paInputs.Qty, paInputs.TP, paInputs.SL,
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

        private void ObserverSubscription(StopAndReverse paTrader)
        {
            //GlobalObjects.ObservableFactory ??= new ObservableFactory();
            //paTrader.Subscribe(GlobalObjects.ObservableFactory);
        }

        //private async Task NMQClientSubscription(StopAndReverse paTrader, uint token)
        //{
        //    zmqClient = new ZMQClient();
        //    zmqClient.AddSubscriber(new List<uint>() { token });

        //    await zmqClient.Subscribe(paTrader);
        //}
#if local
        private async Task KFKClientSubscription(StopAndReverse paTrader, uint token)
        {
            kfkClient = new KSubscriber();
            kfkClient.AddSubscriber(new List<uint>() { token });

            await kfkClient.Subscribe(paTrader);
        }
        //private async Task ObserverSubscription(StopAndReverse paTrader, uint token)
        //{
        //    GlobalObjects.ObservableFactory ??= new ObservableFactory();
        //    paTrader.Subscribe(GlobalObjects.ObservableFactory);
        //}
        //private async Task MQTTCientSubscription(StopAndReverse paTrader, uint token)
        //{
        //    mqttSubscriber = new MQTTSubscriber();
        //    mqttSubscriber.
        //    paTrader.Subscribe(GlobalObjects.ObservableFactory);
        //}
#endif
        //        private void PATrade_OnOptionUniverseChange(StopAndReverse source)
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
        //            return Task.FromResult((int)AlgoIndex.StopAndReverse);
        //        }

        //        [HttpPut("{ain}")]
        //        public bool Put(int ain, [FromBody] int start)
        //        {
        //            List<StopAndReverse> activeAlgoObjects;
        //            if (!_cache.TryGetValue(key, out activeAlgoObjects))
        //            {
        //                activeAlgoObjects = new List<StopAndReverse>();
        //            }

        //            StopAndReverse algoObject = activeAlgoObjects.FirstOrDefault(x => x.AlgoInstance == ain);
        //            if (algoObject != null)
        //            {
        //                algoObject.StopTrade(!Convert.ToBoolean(start));
        //            }
        //            _cache.Set(key, activeAlgoObjects);

        //            return true;
        //        }
    }
}

