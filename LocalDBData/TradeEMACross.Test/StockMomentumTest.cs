using Algorithms.Algorithms;
using Algos.Utilities.Views;
using GlobalLayer;
using System;

namespace LocalDBData.Test
{
    internal class StockMomentumTest : ITest
    {
        StockMomentumOptions paTrader;
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
        private StockMomentumOptions ExecuteAlgo(PriceActionInput paInputs)
        {
            StockMomentumOptions paTrader =
                new StockMomentumOptions(TimeSpan.FromMinutes(paInputs.CTF), paInputs.BToken, paInputs.Qty, paInputs.UID, httpClientFactory: null);

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

        private void ObserverSubscription(StockMomentumOptions paTrader)
        {
            //GlobalObjects.ObservableFactory ??= new ObservableFactory();
            //paTrader.Subscribe(GlobalObjects.ObservableFactory);
        }

        //private async Task NMQClientSubscription(StockMomentum paTrader, uint token)
        //{
        //    zmqClient = new ZMQClient();
        //    zmqClient.AddSubscriber(new List<uint>() { token });

        //    await zmqClient.Subscribe(paTrader);
        //}
#if local
        private async Task KFKClientSubscription(StockMomentum paTrader, uint token)
        {
            kfkClient = new KSubscriber();
            kfkClient.AddSubscriber(new List<uint>() { token });

            await kfkClient.Subscribe(paTrader);
        }
        //private async Task ObserverSubscription(StockMomentum paTrader, uint token)
        //{
        //    GlobalObjects.ObservableFactory ??= new ObservableFactory();
        //    paTrader.Subscribe(GlobalObjects.ObservableFactory);
        //}
        //private async Task MQTTCientSubscription(StockMomentum paTrader, uint token)
        //{
        //    mqttSubscriber = new MQTTSubscriber();
        //    mqttSubscriber.
        //    paTrader.Subscribe(GlobalObjects.ObservableFactory);
        //}
#endif
        //        private void PATrade_OnOptionUniverseChange(StockMomentum source)
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
        //            return Task.FromResult((int)AlgoIndex.StockMomentum);
        //        }

        //        [HttpPut("{ain}")]
        //        public bool Put(int ain, [FromBody] int start)
        //        {
        //            List<StockMomentum> activeAlgoObjects;
        //            if (!_cache.TryGetValue(key, out activeAlgoObjects))
        //            {
        //                activeAlgoObjects = new List<StockMomentum>();
        //            }

        //            StockMomentum algoObject = activeAlgoObjects.FirstOrDefault(x => x.AlgoInstance == ain);
        //            if (algoObject != null)
        //            {
        //                algoObject.StopTrade(!Convert.ToBoolean(start));
        //            }
        //            _cache.Set(key, activeAlgoObjects);

        //            return true;
        //        }
    }
}

