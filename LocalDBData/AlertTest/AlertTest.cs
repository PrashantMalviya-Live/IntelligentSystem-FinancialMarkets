using Algorithms.Algorithms;
using Algorithms.Utilities.Views;
using Algorithms.Utilities;
using Algos.Utilities.Views;
using Global.Web;
using GlobalLayer;
using Google.Apis.Http;
using System;
using System.Collections.Generic;
using System.Data;
using System.Net.Http;
using System.Threading.Tasks;
using System.Runtime.ConstrainedExecution;
using Algorithm.Algorithm;
using Algorithms.Indicators;
using Microsoft.Extensions.Configuration;
using ZMQFacade;
using static Algorithms.Utilities.Utility;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Linq;
using DBAccess;

namespace LocalDBData.Test
{
    public class AlertTest : ITest
    {
        AlertGenerator alertGenerator;
        private const string key = "ACTIVE_EXPIRY_TRADE_OBJECTS";
        IConfiguration configuration;
        static ZMQClient zmqClient;
        private static IMemoryCache _cache;
        static TriggerredAlertData _triggerredAlertData = new TriggerredAlertData();
        private static List<AlertTriggerData> _cacheData;
        private readonly IRDSDAO _rdsDAO;
        public AlertTest(IRDSDAO rdsDAO = null)
        {
            _rdsDAO = rdsDAO;
        }
        public void Execute()
        {
            //The logic should be that when user selects some criteria. The alert monitor should just take all the indicators,
            //and stock universe, time frames, to generate trigger values.
            //There should be another method alert generator that check list of alerttriggercriterion, and generate alerts for users

            _cacheData = Utility.RetrieveAlertTriggerData(_rdsDAO);

            var alertTriggerData = Utility.LoadAlertTriggerData(_cacheData);
            
            //foreach (var alertTriggerCriterion in alertTriggerData.Values)
            //{
            //    foreach(var tfi in alertTriggerCriterion.TimeFrameWithIndicators)
            //    {
            //        tfi.Indicators.ForEach(i =>
            //        {
            //            i.Changed += I_Changed;
            //        });
            //    }
            //}
            
            alertGenerator = new AlertGenerator(alertTriggerData, _rdsDAO);

            alertGenerator.OnOptionUniverseChange += AlertGenerator_OnOptionUniverseChange;
            alertGenerator.OnCriticalEvents += AlertGenerator_OnCriticalEvents;

            //Task task = Task.Run(() => NMQClientSubscription(alertGenerator, 260105));
        }

        private void I_Changed(IIndicatorValue arg1, IIndicatorValue arg2)
        {
            Console.WriteLine(arg1.GetValue<Candle>().CloseTime.Minute - arg1.GetValue<Candle>().OpenTime.Minute);
            Console.WriteLine(arg1.GetValue<Candle>().ClosePrice);
            Console.WriteLine(arg2.Indicator.Id);
            Console.WriteLine(arg2.GetValue<decimal>());
            
            //throw new NotImplementedException();
        }

        ///<summary>
        /// Check all the event critera for this instrument token, indicator and time frame,
        /// </summary>
        /// <param name="instrumentToken"></param>
        /// <param name="indicator"></param>
        /// <param name="timeFrame"></param>
        private void AlertGenerator_OnCriticalEvents(uint instrumentToken, IIndicator indicator, int timeFrame, decimal lastTradePrice)
        {
            //this is list of all the alert trigger criteria active

            List<AlertTriggerData> alertTriggerCriteria = _cacheData;
            if (alertTriggerCriteria == null)
            {
                alertTriggerCriteria = Utility.RetrieveAlertTriggerData(_rdsDAO);
                _cacheData = alertTriggerCriteria;
            }
            //List<AlertTriggerCriterion> alertTriggerCriteria = Utility.LoadAlertCriteria();// new List<AlertTriggerCriterion>();


            UpdateTriggeredData(instrumentToken, indicator, timeFrame);

            //this direct checking of indicators may not work as in distributed application, there may be seperate instance.
            //Put a critical argument to check along with name. Such as length etc.
            //This is temp till peramanent is found.

            //SEND THE ALERT TRIGGER IDS ALONG WITH INDICATOR,AND ALERT CRITERIA IDS, AS IT MAY SLOW DOWN TO CHECK LIKE THE BELOW.
            alertTriggerCriteria.FindAll(x => x.InstrumentToken == instrumentToken && 
            x.AlertCriteria.Any(y=> 
            ((y.Components.Contains(indicator)  && (int)GetPropValue((y.Components.Find(x=> (IIndicator) x ==  indicator)), "TimeSpanInMins") == timeFrame))))
            .ForEach(async atc => 
            {
                //check if registered event is triggered
                if (atc.Triggered())
                {
                    
                    Alert alert = new Alert()
                    {
                        AlertTriggerID = atc.ID,
                        CandleTimeSpan = timeFrame, 
                        InstrumentToken = atc.InstrumentToken,
                        TradingSymbol = atc.TradingSymbol,
                        LastPrice = lastTradePrice,
                        Message = "S924",
                        ID = Guid.NewGuid(),
                        UserId = atc.UserId,
                        AlertModes = "1,2,3,4,5",
                        TriggeredDateTime = DateTime.Now
                    };

                    GlobalCore.AlertCore.PublishAlert(alert);

                    ///TODO: Is it needed to be updated at the same time as alert generation. 
                    ///If yes, then use time db with fast insertion. or collate few 100 alerts 
                    ///and update at regular time, just like you are doing for ticks
                    ///
                    //UPDATE Database with generated alerts
                    await Utility.UpdateGeneratedAlertsAsync(alert, _rdsDAO);
                    //if yes then trigger event for subscribers, another message queue may be
                    //atc.User
                }
            });
        }
        private static void UpdateTriggeredData(uint instrumentToken, IIndicator indicator, int timeFrame)
        {
            if (!_triggerredAlertData.InstrumentTokens.Contains(instrumentToken))
            {
                _triggerredAlertData.InstrumentTokens.Add(instrumentToken);
            }

            if (!_triggerredAlertData.Indicators.Contains(indicator))
            {
                _triggerredAlertData.Indicators.Add(indicator);
            }

            if (!_triggerredAlertData.TimeFramesinMinutes.Contains(timeFrame))
            {
                _triggerredAlertData.TimeFramesinMinutes.Add(timeFrame);
            }

        }

        private static void AlertGenerator_OnOptionUniverseChange(AlertGenerator source)
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
        public void OnNext(Tick tick)
        {
            alertGenerator.OnNext(tick);
        }
        public void StopTrade(bool stopTrade)
        {
            alertGenerator.StopTrade(stopTrade);
        }
        private void SendNotification(string title, string body)
        {

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

       
        private void ObserverSubscription(RangeBreakoutCandle paTrader)
        {
            //GlobalObjects.ObservableFactory ??= new ObservableFactory();
            //paTrader.Subscribe(GlobalObjects.ObservableFactory);
        }

        //private async Task NMQClientSubscription(RangeBreakoutCandle paTrader, uint token)
        //{
        //    zmqClient = new ZMQClient();
        //    zmqClient.AddSubscriber(new List<uint>() { token });

        //    await zmqClient.Subscribe(paTrader);
        //}
#if local
        private async Task KFKClientSubscription(RangeBreakoutCandle paTrader, uint token)
        {
            kfkClient = new KSubscriber();
            kfkClient.AddSubscriber(new List<uint>() { token });

            await kfkClient.Subscribe(paTrader);
        }
        //private async Task ObserverSubscription(RangeBreakoutCandle paTrader, uint token)
        //{
        //    GlobalObjects.ObservableFactory ??= new ObservableFactory();
        //    paTrader.Subscribe(GlobalObjects.ObservableFactory);
        //}
        //private async Task MQTTCientSubscription(RangeBreakoutCandle paTrader, uint token)
        //{
        //    mqttSubscriber = new MQTTSubscriber();
        //    mqttSubscriber.
        //    paTrader.Subscribe(GlobalObjects.ObservableFactory);
        //}
#endif
    }
}

