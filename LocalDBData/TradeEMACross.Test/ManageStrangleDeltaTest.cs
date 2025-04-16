using Algorithms.Algorithms;
using Algos.Utilities.Views;
using GlobalLayer;
using System;

namespace LocalDBData.Test
{
    internal class ManageStrangleDeltaTest : ITest
    {
        ManagedStrangleDelta paTrader;

        public void Execute(StrangleWithDeltaandLevelInputs strangleWithDeltaandLevelInputs)
        {
            paTrader = ExecuteAlgo(strangleWithDeltaandLevelInputs);

            paTrader.OnOptionUniverseChange += ObserverSubscription;
            // paTrader.OnCriticalEvents += SendNotification;// (string title, string body)
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
        private ManagedStrangleDelta ExecuteAlgo(StrangleWithDeltaandLevelInputs strangleWithDeltaandLevelInputs)
        {
            uint instrumentToken = strangleWithDeltaandLevelInputs.BToken;
            //DateTime endDateTime = DateTime.Now;
            //TimeSpan candleTimeSpan = TimeSpan.FromMinutes(optionBuyWithStraddleInput.CTF);

            DateTime expiry = strangleWithDeltaandLevelInputs.Expiry;
            //int optionQuantity = optionBuyWithStraddleInput.Qty;
            TimeSpan CTF = new TimeSpan(0, 5, 0);
            string userId = "PM27031981";
#if local
            //endDateTime = Convert.ToDateTime("2020-10-16 12:21:00");
#endif
            ///FOR ALL STOCKS FUTURE , PASS INSTRUMENTTOKEN AS ZERO. FOR CE/PE ON BNF/NF SEND THE INDEX TOKEN AS INSTRUMENTTOKEN
            ManagedStrangleDelta straddleManager = new ManagedStrangleDelta(instrumentToken, expiry,
            strangleWithDeltaandLevelInputs.IQty, strangleWithDeltaandLevelInputs.StepQty, strangleWithDeltaandLevelInputs.MaxQty, strangleWithDeltaandLevelInputs.TP,
            strangleWithDeltaandLevelInputs.SL, strangleWithDeltaandLevelInputs.IDelta, strangleWithDeltaandLevelInputs.I2Delta, strangleWithDeltaandLevelInputs.MaxDelta, strangleWithDeltaandLevelInputs.MinDelta,
            strangleWithDeltaandLevelInputs.L1, strangleWithDeltaandLevelInputs.L1, strangleWithDeltaandLevelInputs.U1, strangleWithDeltaandLevelInputs.U2, AlgoIndex.DeltaStrangleWithLevels,
            CTF, httpClientFactory: null);

            return straddleManager;
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

        private void ObserverSubscription(ManagedStrangleDelta paTrader)
        {
            //GlobalObjects.ObservableFactory ??= new ObservableFactory();
            //paTrader.Subscribe(GlobalObjects.ObservableFactory);
        }

        //private async Task NMQClientSubscription(CandleWickScalping paTrader, uint token)
        //{
        //    zmqClient = new ZMQClient();
        //    zmqClient.AddSubscriber(new List<uint>() { token });

        //    await zmqClient.Subscribe(paTrader);
        //}
#if local
        private async Task KFKClientSubscription(CandleWickScalping paTrader, uint token)
        {
            kfkClient = new KSubscriber();
            kfkClient.AddSubscriber(new List<uint>() { token });

            await kfkClient.Subscribe(paTrader);
        }
        //private async Task ObserverSubscription(CandleWickScalping paTrader, uint token)
        //{
        //    GlobalObjects.ObservableFactory ??= new ObservableFactory();
        //    paTrader.Subscribe(GlobalObjects.ObservableFactory);
        //}
        //private async Task MQTTCientSubscription(CandleWickScalping paTrader, uint token)
        //{
        //    mqttSubscriber = new MQTTSubscriber();
        //    mqttSubscriber.
        //    paTrader.Subscribe(GlobalObjects.ObservableFactory);
        //}
#endif
        //        private void PATrade_OnOptionUniverseChange(CandleWickScalping source)
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
        //            return Task.FromResult((int)AlgoIndex.CandleWickScalping);
        //        }

        //        [HttpPut("{ain}")]
        //        public bool Put(int ain, [FromBody] int start)
        //        {
        //            List<CandleWickScalping> activeAlgoObjects;
        //            if (!_cache.TryGetValue(key, out activeAlgoObjects))
        //            {
        //                activeAlgoObjects = new List<CandleWickScalping>();
        //            }

        //            CandleWickScalping algoObject = activeAlgoObjects.FirstOrDefault(x => x.AlgoInstance == ain);
        //            if (algoObject != null)
        //            {
        //                algoObject.StopTrade(!Convert.ToBoolean(start));
        //            }
        //            _cache.Set(key, activeAlgoObjects);

        //            return true;
        //        }
    }
}
