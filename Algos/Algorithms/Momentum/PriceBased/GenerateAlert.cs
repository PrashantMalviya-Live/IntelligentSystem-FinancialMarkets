using Algorithms.Candles;
using Algorithms.Indicators;
using Algorithms.Utilities;
using Algorithms.Utils;
using GlobalLayer;
using GlobalCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BrokerConnectWrapper;
using ZMQFacade;
using System.Timers;
using System.Threading;
using System.Net.Sockets;
using Algos.Utilities;
using FirebaseAdmin.Messaging;

namespace Algorithms.Algorithms
{
    public class GenerateAlert : IZMQ
    {
        private readonly int _algoInstance;

        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(GenerateAlert source);
        [field: NonSerialized]
        public event OnOptionUniverseChangeHandler OnOptionUniverseChange;

        [field: NonSerialized]
        public delegate void OnTradeEntryHandler(Order st);
        [field: NonSerialized]
        public event OnTradeEntryHandler OnTradeEntry;

        [field: NonSerialized]
        public delegate void OnTradeExitHandler(Order st);
        [field: NonSerialized]
        public event OnTradeExitHandler OnTradeExit;

        private bool _stopTrade;
        public Queue<uint> TimeCandleWaitingQueue;
        public List<uint> tokenExits;
        //All active tokens that are passing all checks. This is kept seperate from tokenvolume as tokens may get in and out of the activeToken list.
        public List<uint> activeTokens;
        DateTime _endDateTime;
        DateTime? _expiryDate;
        TimeSpan _candleTimeSpan;
        public decimal _strikePriceRange;
        decimal _candleSizeInPercent;
        private uint _baseInstrumentToken;
        private decimal _baseInstrumentPrice;
        public readonly TimeSpan MARKET_START_TIME = new TimeSpan(9, 15, 0);
        public const AlgoIndex algoIndex = AlgoIndex.GenerateAlert;
        //TimeSpan candletimeframe;
        CandleManger candleManger;
        Dictionary<uint, List<Candle>> TimeCandles;
        CentralPivotRange _dailyCPR;
        public List<uint> SubscriptionTokens { get; set; }
        private bool _higherProfit = false;
        private System.Timers.Timer _healthCheckTimer;
        private System.Timers.Timer _logTimer;
        private int _healthCounter = 0;
        private Object tradeLock = new Object();

        private bool _previousDayHigh = false;
        private bool _previousDayLow = false;
        private bool _candleCrossedCPR = false;
        private bool _candleCrossedCPRL = false;
        private bool _candleCrossedCPRU = false;
        private bool _candleCrossedEMA = false;
        private ExponentialMovingAverage _ema;
        private int _emaLength = 5;
        FirebaseMessaging _firebaseMessaging;
        public GenerateAlert(TimeSpan candleTimeSpan,
            uint baseInstrumentToken, decimal candleSizePercent, 
            FirebaseMessaging firebaseMessaging,
            int algoInstance = 0, bool positionSizing = false,
            decimal maxLossPerTrade = 0)
        {
            _firebaseMessaging = firebaseMessaging;
            _candleTimeSpan = candleTimeSpan;
            _candleSizeInPercent = candleSizePercent;
            _baseInstrumentToken = baseInstrumentToken;
            _stopTrade = true;
            SubscriptionTokens = new List<uint>();
            CandleSeries candleSeries = new CandleSeries();
            //DateTime ydayEndTime = _endDateTime.AddDays(-1).Date + new TimeSpan(15, 30, 00);
            TimeCandles = new Dictionary<uint, List<Candle>>();
            candleManger = new CandleManger(TimeCandles, CandleType.Time);
            candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;
            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, baseInstrumentToken, DateTime.Now,
                DateTime.Now, 0, 0, 0, 0,0 ,
                0, 0, 0, 0, candleTimeFrameInMins:
                (float)candleTimeSpan.TotalMinutes, CandleType.Time, 0, candleSizePercent, 0, 0,
                0, 0, positionSizing: false, maxLossPerTrade: 0);

            //ZConnect.Login();

            //health check after 1 mins
            _healthCheckTimer = new System.Timers.Timer(interval: 1 * 60 * 1000);
            _healthCheckTimer.Elapsed += CheckHealth;
            _healthCheckTimer.Start();

            //_ema = new ExponentialMovingAverage(_emaLength);
            //_logTimer = new System.Timers.Timer(interval: 5 * 60 * 1000);
            //_logTimer.Elapsed += PublishLog;
            //_logTimer.Start();
        }

        private void ActiveTradeIntraday(Tick tick)
        {
            DateTime currentTime = (tick.InstrumentToken == _baseInstrumentToken) ?
                tick.Timestamp.Value : tick.LastTradeTime.Value;
            try
            {
                uint token = tick.InstrumentToken;
                lock (tradeLock)
                {
                    if (!GetBaseInstrumentPrice(tick))
                    {
                        return;
                    }

                    if (_dailyCPR == null)
                    {
                        _dailyCPR = LoadDailyCPR();
                    }
                    if(_ema == null)
                    {
                        _ema = LoadEMA();
                    }

                    //LoadOptionsToTrade(currentTime);
                    //UpdateInstrumentSubscription(currentTime);
                    MonitorCandles(tick, currentTime);
                }

                //Closes all postions at 3:29 PM
                //TriggerEODPositionClose(currentTime);

                Interlocked.Increment(ref _healthCounter);
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "ActiveTradeIntraday");
                Thread.Sleep(100);
            }
        }

        private CentralPivotRange LoadDailyCPR()
        {
            List<Historical> historicalCandles = ZObjects.kite.GetHistoricalData(Convert.ToString(_baseInstrumentToken), DateTime.Today.AddDays(-7), DateTime.Today, "day");
            Historical lastHistoricalCandle = historicalCandles[historicalCandles.Count - 1];
            OHLC ohlc = new OHLC();
            ohlc.High = lastHistoricalCandle.High;
            ohlc.Low = lastHistoricalCandle.Low;
            ohlc.Open = lastHistoricalCandle.Open;
            ohlc.Close = lastHistoricalCandle.Close;

            CentralPivotRange dailyCPR = new CentralPivotRange(ohlc);

            return dailyCPR;
        }

        private ExponentialMovingAverage LoadEMA()
        {
            List<Historical> historicalCandles = ZObjects.kite.GetHistoricalData(Convert.ToString(_baseInstrumentToken), DateTime.Today.AddDays(-7), DateTime.Today, "5minute");
            ExponentialMovingAverage ema = new ExponentialMovingAverage(20);

            var historicalCandlese = historicalCandles.Skip(historicalCandles.Count - 25);
            foreach (var candle in historicalCandlese)
            {
               ema.Process(candle.Close, true);
            }

            return ema;
        }

        

        private void MonitorCandles(Tick tick, DateTime currentTime)
        {
            try
            {
                uint token = tick.InstrumentToken;

                //Check the below statement, this should not keep on adding to 
                //TimeCandles with everycall, as the list doesnt return new candles unless built

                if (TimeCandles.ContainsKey(token))
                {
                    candleManger.StreamingTimeFrameCandle(tick, token, _candleTimeSpan, true); // TODO: USING LOCAL VERSION RIGHT NOW
                }
                else
                {
                    DateTime lastCandleEndTime;
                    DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);

                    if (candleStartTime.HasValue)
                    {
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                            String.Format("Starting first Candle now for token: {0}", tick.InstrumentToken), "MonitorCandles");
                        //candle starts from there
                        candleManger.StreamingTimeFrameCandle(tick, token, _candleTimeSpan, true, candleStartTime); // TODO: USING LOCAL VERSION

                    }
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(ex.Message + ex.StackTrace);
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "MonitorCandles");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }
        }
        private void CandleManger_TimeCandleFinished(object sender, Candle e)
        {
            if (e.InstrumentToken == _baseInstrumentToken)
            {
                _ema.Process(e.ClosePrice, true);
                if (Math.Abs(e.ClosePrice - e.OpenPrice) > _candleSizeInPercent * e.OpenPrice / 100)
                {
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, e.CloseTime,
                        String.Format(@"ALERT!! Big Candle came {0}", (e.ClosePrice - e.OpenPrice)), "Candle Finished");
                    Thread.Sleep(100);
                }
                string textMessage;
                if(!_candleCrossedCPR && e.ClosePrice > _dailyCPR.Prices[(int)PivotLevel.CPR])
                {
                    _candleCrossedCPR = true;
                    textMessage = @"ALERT!! Candle Crossed CPR upside";
                    
                    var message = new Message()
                    {
                        Data = new Dictionary<string, string>() { { "Nifty", "17600" }, },
                        Topic = Constants.MARKET_ALERTS,
                        Notification = new Notification() { Title = "Index Alerts", Body = textMessage }
                    };
                    _firebaseMessaging.SendAsync(message);
                    
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, e.CloseTime, textMessage, "Candle Finished");
                    Thread.Sleep(100);
                    //TwilioWhatsApp.SendWhatsAppMessage(textMessage);

                }
                else if(_candleCrossedCPR && e.ClosePrice < _dailyCPR.Prices[(int)PivotLevel.CPR])
                {
                    _candleCrossedCPR = false;
                    textMessage = @"ALERT!! Candle Crossed CPR downside";
                    var message = new Message()
                    {
                        Data = new Dictionary<string, string>() { { "Nifty", "17600" }, },
                        Topic = Constants.MARKET_ALERTS,
                        Notification = new Notification() { Title = "Index Alerts", Body = textMessage }
                    };
                    _firebaseMessaging.SendAsync(message);

                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, e.CloseTime, textMessage, "Candle Finished");
                    Thread.Sleep(100);
                    //TwilioWhatsApp.SendWhatsAppMessage(textMessage);
                }

                if (!_candleCrossedCPRU && e.ClosePrice > _dailyCPR.Prices[(int)PivotLevel.UCPR])
                {
                    _candleCrossedCPRU = true;
                    textMessage = @"ALERT!! Candle Crossed CPRU upside";
                    var message = new Message()
                    {
                        Data = new Dictionary<string, string>() { { "Nifty", "17600" }, },
                        Topic = Constants.MARKET_ALERTS,
                        Notification = new Notification() { Title = "Index Alerts", Body = textMessage }
                    };
                    _firebaseMessaging.SendAsync(message);
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, e.CloseTime, textMessage, "Candle Finished");
                    Thread.Sleep(100);
                    //TwilioWhatsApp.SendWhatsAppMessage(textMessage);
                }
                else if (_candleCrossedCPRU && e.ClosePrice < _dailyCPR.Prices[(int)PivotLevel.UCPR])
                {
                    _candleCrossedCPRU = false;
                    textMessage = @"ALERT!! Candle Crossed CPRU downside";
                    var message = new Message()
                    {
                        Data = new Dictionary<string, string>() { { "Nifty", "17600" }, },
                        Topic = Constants.MARKET_ALERTS,
                        Notification = new Notification() { Title = "Index Alerts", Body = textMessage }
                    };
                    _firebaseMessaging.SendAsync(message);
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, e.CloseTime, textMessage, "Candle Finished");
                    Thread.Sleep(100);
                    //TwilioWhatsApp.SendWhatsAppMessage(textMessage);
                }

                if (!_candleCrossedCPRL && e.ClosePrice > _dailyCPR.Prices[(int)PivotLevel.LCPR])
                {
                    _candleCrossedCPRL = true;
                    textMessage = @"ALERT!! Candle Crossed CPRL upside";
                    var message = new Message()
                    {
                        Data = new Dictionary<string, string>() { { "Nifty", "17600" }, },
                        Topic = Constants.MARKET_ALERTS,
                        Notification = new Notification() { Title = "Index Alerts", Body = textMessage }
                    };
                    _firebaseMessaging.SendAsync(message);
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, e.CloseTime, textMessage, "Candle Finished");
                    Thread.Sleep(100);
                    //TwilioWhatsApp.SendWhatsAppMessage(textMessage);

                }
                else if (_candleCrossedCPRL && e.ClosePrice < _dailyCPR.Prices[(int)PivotLevel.LCPR])
                {
                    _candleCrossedCPRL = false;
                    textMessage = @"ALERT!! Candle Crossed CPRL downside";
                    var message = new Message()
                    {
                        Data = new Dictionary<string, string>() { { "Nifty", "17600" }, },
                        Topic = Constants.MARKET_ALERTS,
                        Notification = new Notification() { Title = "Index Alerts", Body = textMessage }
                    };
                    _firebaseMessaging.SendAsync(message);
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, e.CloseTime, textMessage, "Candle Finished");
                    Thread.Sleep(100);
                    //TwilioWhatsApp.SendWhatsAppMessage(textMessage);
                }

                if (!_candleCrossedEMA && e.ClosePrice > _ema.GetValue<decimal>(0))
                {
                    _candleCrossedEMA = true;
                    textMessage = @"ALERT!! Candle Crossed EMA upside";
                    var message = new Message()
                    {
                        Data = new Dictionary<string, string>() { { "Nifty", "17600" }, },
                        Topic = Constants.MARKET_ALERTS,
                        Notification = new Notification() { Title = "Index Alerts", Body = textMessage }
                    };
                    _firebaseMessaging.SendAsync(message);
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, e.CloseTime, textMessage, "Candle Finished");
                    Thread.Sleep(100);
                    //TwilioWhatsApp.SendWhatsAppMessage(textMessage);
                }
                else if (_candleCrossedEMA && e.ClosePrice < _ema.GetValue<decimal>(0))
                {
                    _candleCrossedEMA = false;
                    textMessage = @"ALERT!! Candle Crossed EMA downside";
                    var message = new Message()
                    {
                        Data = new Dictionary<string, string>() { { "Nifty", "17600" }, },
                        Topic = Constants.MARKET_ALERTS,
                        Notification = new Notification() { Title = "Index Alerts", Body = textMessage }
                    };
                    _firebaseMessaging.SendAsync(message);
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, e.CloseTime, textMessage, "Candle Finished");
                    Thread.Sleep(100);
                    //TwilioWhatsApp.SendWhatsAppMessage(textMessage);
                }
            }
        }

        
        public void StopTrade(bool stop)
        {
            _stopTrade = stop;
        }

        private DateTime? CheckCandleStartTime(DateTime currentTime, out DateTime lastEndTime)
        {
            try
            {
                DateTime? candleStartTime = null;

                if (currentTime.TimeOfDay < MARKET_START_TIME)
                {
                    candleStartTime = DateTime.Now.Date + MARKET_START_TIME;
                    lastEndTime = candleStartTime.Value;
                }
                else
                {

                    double mselapsed = (currentTime.TimeOfDay - MARKET_START_TIME).TotalMilliseconds % _candleTimeSpan.TotalMilliseconds;

                    //if(mselapsed < 1000) //less than a second
                    //{
                    //    candleStartTime =  currentTime;
                    //}
                    if (mselapsed < 60 * 1000)
                    {
                        candleStartTime = currentTime.Date.Add(TimeSpan.FromMilliseconds(currentTime.TimeOfDay.TotalMilliseconds - mselapsed));
                    }
                    //else
                    //{
                    lastEndTime = currentTime.Date.Add(TimeSpan.FromMilliseconds(currentTime.TimeOfDay.TotalMilliseconds - mselapsed));
                    //}
                }

                return candleStartTime;
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "CheckCandleStartTime");
                Thread.Sleep(100);
                Environment.Exit(0);
                lastEndTime = DateTime.Now;
                return null;
            }
        }


       

        public int AlgoInstance
        {
            get
            { return _algoInstance; }
        }
        private bool GetBaseInstrumentPrice(Tick tick)
        {
            Tick baseInstrumentTick = tick.InstrumentToken == _baseInstrumentToken ? tick : null;
            if (baseInstrumentTick != null && baseInstrumentTick.LastPrice != 0)
            {
                _baseInstrumentPrice = baseInstrumentTick.LastPrice;
            }
            if (_baseInstrumentPrice == 0)
            {
                return false;
            }
            return true;
        }
        

        public void OnNext(Tick tick)
        {
            try
            {
                if (_stopTrade || !tick.Timestamp.HasValue)
                {
                    return;
                }
                ActiveTradeIntraday(tick);
                return;
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, tick.Timestamp.GetValueOrDefault(DateTime.UtcNow),
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "OnNext");
                Thread.Sleep(100);
                return;
            }
        }

        private void CheckHealth(object sender, ElapsedEventArgs e)
        {
            //expecting atleast 30 ticks in 1 min
            if (_healthCounter >= 30)
            {
                _healthCounter = 0;
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Health, e.SignalTime, "1", "CheckHealth");
                Thread.Sleep(100);
            }
            else
            {
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Health, e.SignalTime, "0", "CheckHealth");
                Thread.Sleep(100);
            }
        }

    }
}
