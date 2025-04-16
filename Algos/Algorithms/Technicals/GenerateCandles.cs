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


namespace Algorithms.Algorithms
{
    public class GenerateCandles : IZMQ
    {
        CandleType _candleType;
        uint _volumeThreshold;
        decimal _moneyThreshold;
        CandleManger _candleManger;
        Dictionary<uint, List<Candle>> _volumeCandles;
        Dictionary<uint, List<Candle>> _moneyCandles;
        Dictionary<uint, List<Candle>> _timeCandles;
        private bool _start;
        private decimal _baseInstrumentPrice;
        private uint _baseInstrumentToken;
        public SortedList<decimal, Instrument>[] OptionUniverse { get; set; }
        private DateTime _expiry;
        TimeSpan _candleTimeSpan;
        public List<uint> SubscriptionTokens { get; set; }
        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(GenerateCandles source);
        [field: NonSerialized]
        public event OnOptionUniverseChangeHandler OnOptionUniverseChange;
        public readonly TimeSpan MARKET_START_TIME = new TimeSpan(9, 15, 0);

        public GenerateCandles(DateTime expiry, uint volumeThreshold, decimal moneyThreshold, TimeSpan candleTimeSpan)
        {
            _baseInstrumentToken = 260105;
            _volumeThreshold = volumeThreshold;
            _moneyThreshold = moneyThreshold;
            _candleTimeSpan = candleTimeSpan;
            _expiry = expiry;
            _volumeCandles = new Dictionary<uint, List<Candle>>();
            _moneyCandles = new Dictionary<uint, List<Candle>>();
            _timeCandles = new Dictionary<uint, List<Candle>>();

            _candleManger = new CandleManger();

            _candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;
            _candleManger.VolumeCandleFinished += CandleManger_VolumeCandleFinished;
            _candleManger.MoneyCandleFinished += CandleManger_MoneyCandleFinished;
            _start = true;

            SubscriptionTokens = new List<uint>();

        }

        private void Start(Tick tick)
        {
            DateTime currentTime = tick.InstrumentToken == _baseInstrumentToken ?
                tick.Timestamp.Value : tick.LastTradeTime.Value;
            try
            {
                uint token = tick.InstrumentToken;
                lock (_volumeCandles)
                {
                    if (!GetBaseInstrumentPrice(tick))
                    {
                        return;
                    }
                    LoadOptionsToTrade(currentTime);
                    UpdateInstrumentSubscription(currentTime);
                    //DataAccess.MarketDAO dao = new DataAccess.MarketDAO();
                    //Queue<Tick> tickQueue = new Queue<Tick>();
                    //tickQueue.Enqueue(tick);
                    //dao.StoreTickData(tickQueue);

                    //if (!GetBaseInstrumentPrice(tick))
                    //{
                    //    return;
                    //}

                    //LoadOptionsToTrade(currentTime);
                    //UpdateInstrumentSubscription(currentTime);
                    MonitorVolumeCandles(tick, currentTime, _volumeCandles);
                    MonitorMoneyCandles(tick, currentTime, _moneyCandles);
                    //MonitorTimeCandles(tick, currentTime, _timeCandles);
                }
            }
            catch
            {

            }
        }
        //public void Start(Tick[] ticks)
        //{
        //    List<Candle> volumeCandles = _candleManger.StreamingVolumeCandle(ticks, _instrumentToken, 10000); // TODO: USING LOCAL VERSION RIGHT NOW
        //}

        private void UpdateInstrumentSubscription(DateTime currentTime)
        {
            try
            {
                bool dataUpdated = false;
                if (OptionUniverse != null)
                {
                    foreach (var options in OptionUniverse)
                    {
                        foreach (var option in options)
                        {
                            if (!SubscriptionTokens.Contains(option.Value.InstrumentToken))
                            {
                                SubscriptionTokens.Add(option.Value.InstrumentToken);
                                dataUpdated = true;
                            }
                        }
                    }
                    if (dataUpdated)
                    {
                        Task task = Task.Run(() => OnOptionUniverseChange(this));
                    }
                }
            }
            catch (Exception ex)
            {
                _start = false;
                Logger.LogWrite(ex.Message + ex.StackTrace);
                Logger.LogWrite("Closing Application");
                Thread.Sleep(100);
            }
        }

        private void MonitorTimeCandles(Tick tick, DateTime currentTime, Dictionary<uint, List<Candle>> candles)
        {
            try
            {
                uint token = tick.InstrumentToken;

                if (candles.ContainsKey(token))
                {
                    candles[token] = _candleManger.StreamingTimeFrameCandle(tick, token, _candleTimeSpan, true); // TODO: USING LOCAL VERSION RIGHT NOW
                }
                else
                {
                    DateTime lastCandleEndTime;
                    DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);

                    if (candleStartTime.HasValue)
                    {
                        //candle starts from there
                        candles.Add(token, _candleManger.StreamingTimeFrameCandle(tick, token, _candleTimeSpan, true, candleStartTime));
                    }
                }
            }
            catch (Exception ex)
            {
                _start = false;
                Logger.LogWrite(ex.Message + ex.StackTrace);
                Logger.LogWrite("Trading Stopped as algo encountered an error");
            }
        }
        private void MonitorVolumeCandles(Tick tick, DateTime currentTime, Dictionary<uint, List<Candle>> candles)
        {
            try
            {
                uint token = tick.InstrumentToken;

                if (candles.ContainsKey(token))
                {
                    candles[token] = _candleManger.StreamingVolumeCandle(tick, token, _volumeThreshold); // TODO: USING LOCAL VERSION RIGHT NOW
                }
                else
                {
                    DateTime lastCandleEndTime;
                    DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);

                    if (candleStartTime.HasValue)
                    {
                        //candle starts from there
                        candles.Add(token, _candleManger.StreamingVolumeCandle(tick, token, _volumeThreshold));
                    }
                }
            }
            catch (Exception ex)
            {
                _start = false;
                Logger.LogWrite(ex.Message + ex.StackTrace);
                Logger.LogWrite("Trading Stopped as algo encountered an error");
            }
        }
        private void MonitorMoneyCandles(Tick tick, DateTime currentTime, Dictionary<uint, List<Candle>> candles)
        {
            try
            {
                uint token = tick.InstrumentToken;

                if (candles.ContainsKey(token))
                {
                    candles[token] = _candleManger.StreamingMoneyCandle(tick, token, _moneyThreshold); // TODO: USING LOCAL VERSION RIGHT NOW
                }
                else
                {
                    DateTime lastCandleEndTime;
                    DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);

                    if (candleStartTime.HasValue)
                    {
                        //candle starts from there
                        candles.Add(token, _candleManger.StreamingMoneyCandle(tick, token, _moneyThreshold));
                    }
                }
            }
            catch (Exception ex)
            {
                _start = false;
                Logger.LogWrite(ex.Message + ex.StackTrace);
                Logger.LogWrite("Trading Stopped as algo encountered an error");
            }
        }

        public void OnNext(Tick tick)
        {
            try
            {
                if (_start  && tick.Timestamp.HasValue)
                {
                    Start(tick);
                    return;
                }
                else
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                _start = false;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Candle generation stopped.");
                Thread.Sleep(100);
                return;
            }
        }

        private void CandleManger_MoneyCandleFinished(object sender, Candle e)
        {
            DataLogic dataLogic = new DataLogic();
            dataLogic.SaveCandle(e);
        }

        private void CandleManger_VolumeCandleFinished(object sender, Candle e)
        {
            DataLogic dataLogic = new DataLogic();
            dataLogic.SaveCandle(e);
        }

        private void CandleManger_TimeCandleFinished(object sender, Candle e)
        {
            DataLogic dataLogic = new DataLogic();
            dataLogic.SaveCandle(e);
        }

        private void LoadOptionsToTrade(DateTime currentTime)
        {
            try
            {
                var ceStrike = Math.Floor(_baseInstrumentPrice / 100m) * 100m;
                var peStrike = Math.Ceiling(_baseInstrumentPrice / 100m) * 100m;

                DataLogic dl = new DataLogic();
                Dictionary<uint, uint> mappedTokens;
                if (OptionUniverse == null ||
                (OptionUniverse[(int)InstrumentType.PE].Keys.Last() <= _baseInstrumentPrice + 0
                || OptionUniverse[(int)InstrumentType.PE].Keys.First() >= _baseInstrumentPrice + 200)
                   || (OptionUniverse[(int)InstrumentType.CE].Keys.First() >= _baseInstrumentPrice - 0
                   || OptionUniverse[(int)InstrumentType.CE].Keys.Last() <= _baseInstrumentPrice - 200)
                    )
                {
                    //Load options asynchronously
                    var closeOptions = dl.LoadCloseByOptions(_expiry, _baseInstrumentToken, _baseInstrumentPrice, Math.Max(200, 200), out mappedTokens); //loading at least 600 tokens each side

                    UpsertOptionUniverse(closeOptions);

                }

            }
            catch (Exception ex)
            {
                _start = false;
                Logger.LogWrite(ex.Message + ex.StackTrace);
                Logger.LogWrite("Closing Application");
                Thread.Sleep(100);
            }
        }
        private void UpsertOptionUniverse(SortedList<decimal, Instrument>[] closeOptions)
        {
            OptionUniverse ??= new SortedList<decimal, Instrument>[2];
            for (int it = 0; it < 2; it++)
            {
                for (int i = 0; i < closeOptions[it].Count; i++)
                {
                    decimal strike = closeOptions[it].ElementAt(i).Key;
                    if (!(OptionUniverse[it] ??= new SortedList<decimal, Instrument>()).ContainsKey(strike))
                    {
                        OptionUniverse[it].Add(strike, closeOptions[it].ElementAt(i).Value);
                    }
                }
            }
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
                _start = false;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
                lastEndTime = DateTime.Now;
                return null;
            }
        }
        public void StopTrade(bool stop)
        {
            //_stopTrade = stop;
        }
        private bool GetBaseInstrumentPrice(Tick tick)
        {
            Tick baseInstrumentTick = tick.InstrumentToken == _baseInstrumentToken ? tick : null;
            if (baseInstrumentTick != null && baseInstrumentTick.LastPrice != 0)  //(strangleNode.BaseInstrumentPrice == 0)// * callOption.LastPrice * putOption.LastPrice == 0)
            {
                _baseInstrumentPrice = baseInstrumentTick.LastPrice;
            }
            if (_baseInstrumentPrice == 0)
            {
                return false;
            }
            return true;
        }
    }
}
