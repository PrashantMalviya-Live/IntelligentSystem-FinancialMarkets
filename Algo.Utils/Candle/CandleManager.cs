using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GlobalLayer;
using MathNet.Numerics.Statistics;

namespace Algorithms.Utils
{
    public class CandleManger
    {
        //Get Candles
        //prepare and store candles
        //Save in data base
        //1 min candels
        //merge candles and return based on time series
        //parameter query such as top /bottom/line etc

        Dictionary<UInt32, List<Candle>> MoneyCandles = new Dictionary<uint, List<Candle>>();
        Dictionary<UInt32, List<Tick>> MarketTicks = new Dictionary<uint, List<Tick>>();
        Dictionary<UInt32, List<Candle>> VolumeCandles = new Dictionary<uint, List<Candle>>();
        Dictionary<UInt32, List<Candle>> TimeFrameCandles = new Dictionary<uint, List<Candle>>();

        Dictionary<UInt32, Tick> moneyPrevTicks = new Dictionary<uint, Tick>();
        Dictionary<UInt32, Tick> volumePrevTicks = new Dictionary<uint, Tick>();
        Dictionary<UInt32, Tick> timePrevTicks = new Dictionary<uint, Tick>();

        public event EventHandler<Candle> TimeCandleFinished;
        public event EventHandler<Candle> VolumeCandleFinished;
        public event EventHandler<Candle> MoneyCandleFinished;

        private readonly Stopwatch timer = new Stopwatch();

        public CandleManger(Dictionary<UInt32, List<Candle>> candleList, CandleType candleType)
        {
            switch (candleType)
            {
                case CandleType.Time:
                    TimeFrameCandles = candleList;
                    break;
                case CandleType.Volume:
                    VolumeCandles = candleList;
                    break;
                case CandleType.Money:
                    MoneyCandles = candleList;
                    break;
            }
        }

        //private TimeSpan _timeSpan;// = new TimeSpan(0, 0, 1);
        //private uint _volumeLimit, _volumeTraded;
        //private TimeSpan _timeLimit, _timeTraded;
        //private decimal _moneyLimit, _moneyExchanged;

        /// <summary>
        /// This will store ticks temporarily till Timer time outs
        /// </summary>
        /// <param name="ticks"></param>
        //private void InputTickStream(Tick[] ticks)
        //{
        //    foreach (Tick Tickdata in ticks)
        //    {
        //        if (MarketTicks.ContainsKey(Tickdata.InstrumentToken))
        //        {
        //            MarketTicks[Tickdata.InstrumentToken].Add(Tickdata);
        //        }
        //        else
        //        {
        //            List<Tick> tickList = new List<Tick>();
        //            tickList.Add(Tickdata);
        //            MarketTicks.Add(Tickdata.InstrumentToken, tickList);
        //        }

        //        if (MarketTicks[Tickdata.InstrumentToken].Sum(x=>x.Volume) > _volumeLimit)
        //        {
        //            VolumeThresholdReached?.Invoke(this, new Tuple<UInt32, List<Tick>>(Tickdata.InstrumentToken, MarketTicks[Tickdata.InstrumentToken]));
        //        }
        //        //if (MarketTicks[Tickdata.InstrumentToken].Sum(x => x.LastPrice) > _moneyLimit)
        //        //{
        //        //    MoneyThresholdReached?.Invoke(this, new Tuple<UInt32, List<Tick>>(Tickdata.InstrumentToken, MarketTicks[Tickdata.InstrumentToken]));
        //        //}


        //    }
        //}
        /// <summary>
        /// This function will determine the next candle start depending on market opening time.
        /// </summary>
        /// <param name="timeSpan"></param>
        private void SetStartTime(TimeSpan timeSpan)
        {
            double delayTime = timeSpan.TotalMilliseconds - ((DateTime.Now - DateTime.Today.AddMinutes(9 * 60 + 15)).TotalMilliseconds) % timeSpan.TotalMilliseconds; //Start at 9:15
            Task.Delay(Convert.ToInt32(delayTime)).ContinueWith(_ =>
            {
                // InitTimer();
                StartStopWatch();
            });

        }

        private void StartStopWatch()
        {
            timer.Start();
        }
        //public CandleManger()//TimeSpan timeFrame)
        //{
        //    // _timeSpan = timeFrame;
        //    //SetStartTime();
        //    //StartStopWatch();
        //}
        public CandleManger()
        {
        }
        public CandleManger(uint volumeThreshold)
        {
           //_volumeTraded = _volumeLimit = volumeThreshold;
            //VolumeThresholdReached += OnVolumeThresholdReached; 
        }
        public CandleManger(decimal moneyThreshold)
        {
           // _moneyExchanged = _moneyLimit = moneyThreshold;
            //MoneyThresholdReached += OnMoneyThresholdReached;
            //MoneyThresholdReached += GetNewMoneyCandle;
        }

        //public void InitTimer(TimeSpan timeSpan)
        //{
        //    GlobalObjects.OHLCTimer = new System.Timers.Timer(timeSpan.Milliseconds); // 1 min
        //    GlobalObjects.OHLCTimer.Start();

        //    GlobalObjects.OHLCTimer.Elapsed += CandleTimerEvent;
        //}
        //private void CandleTimerEvent(object sender, System.Timers.ElapsedEventArgs e)
        //{
        //    Dictionary<UInt32, List<Tick>> localMarketTicks;
        //    lock (MarketTicks)
        //    {
        //        localMarketTicks = MarketTicks;
        //        MarketTicks.Clear();
        //    }

        //    foreach (uint token in localMarketTicks.Keys)
        //    {
        //        GenerateTimeFrameCandles(token, MarketTicks[token]);
        //        //generate event that the candle has been created. Since this is 1 min time fram thing and not HFT, event handling is fine.
        //        //There could be may classes (algorithms) that can subscribe to this event.
        //    }

        //}

        //private TimeFrameCandle GenerateTimeFrameCandles(uint token, List<Tick> ticks, TimeSpan timeSpan)
        //{
        //    Tick lasttick = ticks.Last();
        //    Tick firsttick = ticks.First();
        //    TimeSpan timeTraded = new TimeSpan(0, 0, 0);
        //    TimeFrameCandle timeFrameCandle = new TimeFrameCandle() {
        //        Arg = new TimeSpan(0, 0, 0),
        //        ClosePrice = lasttick.Close,
        //        CloseTime = lasttick.Timestamp.Value,
        //        CloseVolume = lasttick.Volume,
        //        OpenPrice = firsttick.Close,
        //        OpenTime = firsttick.Timestamp.Value,
        //        OpenVolume = firsttick.Volume,
        //        OpenInterest = lasttick.OI
        //    };

        //    decimal maxPrice = ticks[0].LastPrice, minPrice = ticks[0].LastPrice;
        //    decimal maxVolume = ticks[0].Volume, priceatMaxVol = ticks[0].LastPrice;
        //    uint volatMaxPrice = ticks[0].Volume, volatMinPrice = ticks[0].Volume;
        //    uint totalVolume = ticks[0].Volume;
        //    DateTime? timeatMaxPrice = ticks[0].Timestamp, timeatMinPrice = ticks[0].Timestamp;
        //    int upTicks = 0;
        //    int downTicks = 0;
        //    uint oi = ticks[0].OI;
        //    Tick prevTick;
        //    decimal currentPrice = ticks[0].LastPrice;
        //    decimal prevPrice = ticks[0].LastPrice;

        //    CandlePriceLevel priceLevel = new CandlePriceLevel(prevPrice);
        //    CandlePriceLevel MaxPriceLevel = new CandlePriceLevel(maxPrice);
        //    CandlePriceLevel MinPriceLevel = new CandlePriceLevel(minPrice);
        //    List<CandlePriceLevel> priceLevels = new List<CandlePriceLevel>();

        //    priceLevels.Add(priceLevel);

        //    for (int i = 1; i < ticks.Count; i++)
        //    {
        //        Tick tick = ticks[i];
        //        prevTick = ticks[i - 1];
        //        currentPrice = tick.LastPrice;
        //        totalVolume += tick.Volume;
        //        if (tick.LastPrice >= maxPrice)
        //        {
        //            maxPrice = tick.LastPrice;
        //            timeatMaxPrice = tick.Timestamp;
        //            volatMaxPrice = tick.LastPrice == maxPrice ? volatMaxPrice + tick.Volume : tick.Volume;
        //        }
        //        if (tick.LastPrice <= minPrice)
        //        {
        //            minPrice = tick.LastPrice;
        //            timeatMinPrice = tick.Timestamp;
        //            volatMinPrice = tick.LastPrice == minPrice ? volatMinPrice + tick.Volume : tick.Volume;
        //        }

        //        UpdatePriceLevel(priceLevels, prevPrice, prevTick.Volume, uptrend: currentPrice > prevPrice);
        //        if (currentPrice > prevPrice)upTicks++;else downTicks++;
        //    }

        //    timeFrameCandle.DownTicks = downTicks;
        //    timeFrameCandle.UpTicks = upTicks;
        //    timeFrameCandle.HighPrice = maxPrice;
        //    timeFrameCandle.HighTime = timeatMaxPrice.Value;
        //    timeFrameCandle.HighVolume = volatMaxPrice;

        //    timeFrameCandle.LowPrice = minPrice;
        //    timeFrameCandle.LowTime = timeatMaxPrice.Value;
        //    timeFrameCandle.LowVolume = volatMinPrice;

        //    timeFrameCandle.PriceLevels = priceLevels;
        //    timeFrameCandle.State = CandleStates.Finished;

        //    timeFrameCandle.TimeFrame = _timeSpan;
        //    timeFrameCandle.TotalTicks = upTicks + downTicks;
        //    timeFrameCandle.TotalVolume = totalVolume;

        //    return timeFrameCandle;
        //}

        //public List<Candle> StreamingTimeFrameCandle(Tick tick, uint token, TimeSpan timeFrame)
        //{
        //    //foreach (Tick tick in ticks)
        //    //{
        //        TimeFrameCandle timeCandle = null;

        //        TimeSpan timeTraded = timer.Elapsed;

        //        if (TimeFrameCandles.ContainsKey(token))
        //        {
        //            timeCandle = TimeFrameCandles[token].LastOrDefault(x => x.State == CandleStates.Inprogress) as TimeFrameCandle;
        //            //timeTraded -= (TimeSpan)timeCandle.Arg2;
        //        }

        //        if (timeCandle != null)
        //        {
        //            timeCandle.ClosePrice = tick.LastPrice;
        //            timeCandle.CloseTime = tick.Timestamp.Value;
        //            timeCandle.CloseVolume = Convert.ToDecimal(tick.Volume);
        //            timeCandle.State = CandleStates.Inprogress;
        //            //timeCandle.Arg = timer.Elapsed;
        //            if (tick.LastPrice >= timeCandle.HighPrice)
        //            {
        //                timeCandle.HighPrice = tick.LastPrice;
        //                timeCandle.HighTime = tick.Timestamp.Value;
        //                timeCandle.HighVolume = tick.LastPrice == timeCandle.HighPrice ? timeCandle.HighVolume + Convert.ToDecimal(tick.Volume) : Convert.ToDecimal(tick.Volume);
        //            }
        //            if (tick.LastPrice <= timeCandle.LowPrice)
        //            {
        //                timeCandle.LowPrice = tick.LastPrice;
        //                timeCandle.LowTime = tick.Timestamp.Value;
        //                timeCandle.LowVolume = tick.LastPrice == timeCandle.LowPrice ? timeCandle.LowVolume + Convert.ToDecimal(tick.Volume) : Convert.ToDecimal(tick.Volume);
        //            }

        //            Tick prevTick = timePrevTicks[token];
        //            if (tick.LastPrice > prevTick.LastPrice) timeCandle.UpTicks++; else timeCandle.DownTicks++;

        //            //TODO: Money Candle Price level should get updated.
        //            UpdatePriceLevel(timeCandle.PriceLevels.ToList(), prevTick.LastPrice, (Convert.ToDecimal(tick.Volume) - Convert.ToDecimal(prevTick.Volume)), tick.LastPrice);

        //            timePrevTicks[token] = tick;
        //            //timeCandles[token].Add(timeCandle); TODO: The list should get updated automatically

        //            //TimeSpan timeTraded = System.DateTime.Now - timeCandle.OpenTime;
        //            if (timeTraded >= timeFrame)
        //            {
        //                //Close the candle and trigger event Time Candle Finished
        //                timeCandle.State = CandleStates.Finished; //TODO: the list should get updated autmatically
        //                                                          //timeCandles[token].Add(timeCandle); 
        //                TimeCandleFinished(this, timeCandle);
        //                timer.Restart();
        //            }

        //        }
        //        else
        //        {
        //            if(!timer.IsRunning)
        //            {
        //                timer.Start();
        //            }
        //            timeCandle = new TimeFrameCandle()
        //            {
        //                //Arg2 = tick.Timestamp.Value, //Start Time
        //                OpenPrice = tick.LastPrice,
        //                OpenTime =  System.DateTime.Now, //tick.Timestamp.Value, // For testing purpose tick timestamp is better
        //                OpenVolume = Convert.ToDecimal(tick.Volume),
        //                ClosePrice = tick.LastPrice,
        //                CloseTime = tick.Timestamp.Value,
        //                CloseVolume = Convert.ToDecimal(tick.Volume),
        //                HighPrice = tick.LastPrice,
        //                HighTime = tick.Timestamp.Value,
        //                HighVolume = Convert.ToDecimal(tick.Volume),
        //                LowPrice = tick.LastPrice,
        //                LowTime = tick.Timestamp.Value,
        //                LowVolume = Convert.ToDecimal(tick.Volume),
        //                OpenInterest = tick.OI,
        //                DownTicks = 0,
        //                UpTicks = 0,
        //                State = CandleStates.Inprogress,
        //                TotalTicks = 0,
        //                TimeFrame = timeFrame
        //            };

        //            List<CandlePriceLevel> priceLevels = new List<CandlePriceLevel>();
        //            priceLevels.Add(new CandlePriceLevel(tick.LastPrice));
        //            timeCandle.PriceLevels = priceLevels;

        //            if (TimeFrameCandles.ContainsKey(token))
        //            {
        //                TimeFrameCandles[token].Add(timeCandle);
        //                timePrevTicks[token] = tick;
        //            }
        //            else
        //            {
        //                List<Candle> timeCandleList = new List<Candle>();
        //                timeCandleList.Add(timeCandle);
        //                TimeFrameCandles.Add(token, timeCandleList);
        //                timePrevTicks.Add(token, tick);
        //            }
        //        }

        //        #region commented code
        //        //if (timeTraded >= timeFrame && timeCandle != null)
        //        //{
        //        //    //Close the candle and trigger event Time Candle Finished

        //        //    timeCandle.State = CandleStates.Finished; //TODO: the list should get updated autmatically
        //        //                                              //timeCandles[token].Add(timeCandle); 
        //        //    TimeCandleFinished(this, timeCandle);
        //        //}

        //        ////IF timeCandle threshold is reached, trigger an event and call get initial candle set up with initial tick data for open parameters
        //        //if (timeTraded >= timeFrame || timeCandle == null) //Stop watch
        //        //{
        //        //    if (timeCandle != null)
        //        //    {
        //        //        timeCandle.State = CandleStates.Finished; //TODO: the list should get updated autmatically
        //        //        //timeCandles[token].Add(timeCandle); 
        //        //        TimeCandleFinished(this, timeCandle);
        //        //    }
        //        //    timer.Restart();
        //        //    timeCandle = new TimeFrameCandle()
        //        //    {
        //        //        Arg2 = tick.Timestamp.Value,
        //        //        Arg = tick.Timestamp.Value,
        //        //        OpenPrice = tick.LastPrice,
        //        //        OpenTime = tick.Timestamp.Value,
        //        //        OpenVolume = tick.Volume,
        //        //        ClosePrice = tick.LastPrice,
        //        //        CloseTime = tick.Timestamp.Value,
        //        //        CloseVolume = tick.Volume,
        //        //        HighPrice = tick.LastPrice,
        //        //        HighTime = tick.Timestamp.Value,
        //        //        HighVolume = tick.Volume,
        //        //        LowPrice = tick.LastPrice,
        //        //        LowTime = tick.Timestamp.Value,
        //        //        LowVolume = tick.Volume,
        //        //        OpenInterest = tick.OI,
        //        //        DownTicks = 0,
        //        //        UpTicks = 0,
        //        //        State = CandleStates.Inprogress,
        //        //        TotalTicks = 0,
        //        //        TimeFrame = timeFrame
        //        //    };

        //        //    List<CandlePriceLevel> priceLevels = new List<CandlePriceLevel>();
        //        //    priceLevels.Add(new CandlePriceLevel(tick.LastPrice));
        //        //    timeCandle.PriceLevels = priceLevels;

        //        //    if (TimeFrameCandles.ContainsKey(token))
        //        //    {
        //        //        TimeFrameCandles[token].Add(timeCandle);
        //        //        timePrevTicks[token] = tick;
        //        //    }
        //        //    else
        //        //    {
        //        //        List<TimeFrameCandle> timeCandleList = new List<TimeFrameCandle>();
        //        //        timeCandleList.Add(timeCandle);
        //        //        TimeFrameCandles.Add(token, timeCandleList);
        //        //        timePrevTicks.Add(token, tick);
        //        //    }

        //        //    //MarketTicks[token].Add(tick);
        //        //}
        //        //else
        //        //{
        //        //    timeCandle.ClosePrice = tick.LastPrice;
        //        //    timeCandle.CloseTime = tick.Timestamp.Value;
        //        //    timeCandle.CloseVolume = tick.Volume;
        //        //    timeCandle.State = CandleStates.Inprogress;
        //        //    //timeCandle.Arg = timer.Elapsed;
        //        //    if (tick.LastPrice >= timeCandle.HighPrice)
        //        //    {
        //        //        timeCandle.HighPrice = tick.LastPrice;
        //        //        timeCandle.HighTime = tick.Timestamp.Value;
        //        //        timeCandle.HighVolume = tick.LastPrice == timeCandle.HighPrice ? timeCandle.HighVolume + tick.Volume : tick.Volume;
        //        //    }
        //        //    if (tick.LastPrice <= timeCandle.LowPrice)
        //        //    {
        //        //        timeCandle.LowPrice = tick.LastPrice;
        //        //        timeCandle.LowTime = tick.Timestamp.Value;
        //        //        timeCandle.LowVolume = tick.LastPrice == timeCandle.LowPrice ? timeCandle.LowVolume + tick.Volume : tick.Volume;
        //        //    }

        //        //    Tick prevTick = timePrevTicks[token];
        //        //    if (tick.LastPrice > prevTick.LastPrice) timeCandle.UpTicks++; else timeCandle.DownTicks++;

        //        //    //TODO: Money Candle Price level should get updated.
        //        //    UpdatePriceLevel(timeCandle.PriceLevels.ToList(), prevTick.LastPrice, prevTick.Volume, uptrend: tick.LastPrice > prevTick.LastPrice);

        //        //    timePrevTicks[token] = tick;
        //        //    //timeCandles[token].Add(timeCandle); TODO: The list should get updated automatically
        //        //}
        //        #endregion
        //    //}
        //    return TimeFrameCandles[token];
        //}

        /// <summary>
        /// Full detailed Time frame candle
        /// </summary>
        /// <param name="ticks"></param>
        /// <param name="token"></param>
        /// <param name="timeFrame"></param>
        /// <param name="localTicks"></param>
        /// <param name="CandleStartTime"></param>
        /// <returns></returns>
        public List<Candle> StreamingTimeFrameCandle(Tick tick, uint token, TimeSpan timeFrame, bool localTicks, DateTime? CandleStartTime = null)
        {
            DateTime tickTime = tick.LastTradeTime ?? tick.Timestamp.Value;
            TimeFrameCandle previousCandle = null;
            //foreach (Tick tick in ticks)
            //{
                TimeFrameCandle timeCandle = null;

                //TimeSpan timeTraded = timer.Elapsed;
                
                if (TimeFrameCandles.ContainsKey(token))
                {
                    timeCandle = TimeFrameCandles[token].LastOrDefault(x => x.State == CandleStates.Inprogress) as TimeFrameCandle;

                    previousCandle = TimeFrameCandles[token].LastOrDefault(x => x.State == CandleStates.Finished) as TimeFrameCandle;
                    //timeTraded -= (TimeSpan)timeCandle.Arg2;
                }
                if (timeCandle == null || tickTime >= (timeCandle.OpenTime + timeFrame))
                {
                    if (timeCandle != null)
                    {
                        //Close the candle and trigger event Time Candle Finished
                        timeCandle.State = CandleStates.Finished; //TODO: the list should get updated autmatically
                        timeCandle.CloseTime = timeCandle.OpenTime + timeFrame;
                        timeCandle.TotalTicks = timeCandle.DownTicks + timeCandle.UpTicks;
                        //timeCandles[token].Add(timeCandle); 
                        TimeCandleFinished(this, timeCandle);
                        //timer.Restart();
                        previousCandle = timeCandle;
                    }

                    //Create new candle and move the tick there
                    timeCandle = new TimeFrameCandle()
                    {
                        //Arg2 = tick.Timestamp.Value, //Start Time
                        InstrumentToken = token,
                        OpenPrice = tick.LastPrice,
                        OpenTime = (previousCandle == null || previousCandle.CloseTime.Date != tickTime.Date) ? 
                        CandleStartTime.HasValue ? CandleStartTime.Value : tickTime : previousCandle.CloseTime, // For testing purpose tick timestamp is better
                        OpenVolume = timePrevTicks.ContainsKey(token) ? Convert.ToDecimal(tick.Volume) - Convert.ToDecimal(timePrevTicks[token].Volume) : 0,
                        ClosePrice = tick.LastPrice,
                        CloseTime = tickTime,
                        CloseVolume = timePrevTicks.ContainsKey(token) ? Convert.ToDecimal(tick.Volume) - Convert.ToDecimal(timePrevTicks[token].Volume) : 0,
                        HighPrice = tick.LastPrice,
                        HighTime = tickTime,
                        HighVolume = timePrevTicks.ContainsKey(token) ? Convert.ToDecimal(tick.Volume) - Convert.ToDecimal(timePrevTicks[token].Volume) : 0,
                        LowPrice = tick.LastPrice,
                        LowTime = tickTime,
                        LowVolume = timePrevTicks.ContainsKey(token) ? Convert.ToDecimal(tick.Volume) - Convert.ToDecimal(timePrevTicks[token].Volume) : 0,
                        OpenInterest = tick.OI,
                        DownTicks = previousCandle != null && previousCandle.ClosePrice > tick.LastPrice ? 1 : 0,
                        UpTicks = previousCandle != null && previousCandle.ClosePrice < tick.LastPrice ? 1 : 0,
                        State = CandleStates.Inprogress,
                        TotalTicks = 0,
                        TimeFrame = timeFrame,
                        TotalVolume = timePrevTicks.ContainsKey(token) ? Convert.ToDecimal(tick.Volume) - Convert.ToDecimal(timePrevTicks[token].Volume) : 0
                    };

                    List<CandlePriceLevel> priceLevels = new List<CandlePriceLevel>();
                    priceLevels.Add(new CandlePriceLevel(tick.LastPrice));
                    timeCandle.PriceLevels = priceLevels;

                    if (TimeFrameCandles.ContainsKey(token))
                    {
                        TimeFrameCandles[token].Add(timeCandle);
                        timePrevTicks[token] = tick;
                    }
                    else
                    {
                        List<Candle> timeCandleList = new List<Candle>();
                        timeCandleList.Add(timeCandle);
                        TimeFrameCandles.Add(token, timeCandleList);
                        timePrevTicks.Add(token, tick);
                    }

                }

                else if (timeCandle != null)
                {
                    Tick prevTick = timePrevTicks[token];

                    timeCandle.ClosePrice = tick.LastPrice;
                    timeCandle.CloseTime = tickTime;
                    timeCandle.CloseVolume += (Convert.ToDecimal(tick.Volume) - Convert.ToDecimal(prevTick.Volume));
                    timeCandle.State = CandleStates.Inprogress;
                    //timeCandle.Arg = timer.Elapsed;
                    if (tick.LastPrice >= timeCandle.HighPrice)
                    {
                        timeCandle.HighPrice = tick.LastPrice;
                        timeCandle.HighTime = tickTime;
                        timeCandle.HighVolume = tick.LastPrice == timeCandle.HighPrice ? 
                            timeCandle.HighVolume + Convert.ToDecimal(tick.Volume) - Convert.ToDecimal(prevTick.Volume) 
                            : Convert.ToDecimal(tick.Volume) - Convert.ToDecimal(prevTick.Volume);
                    }
                    if (tick.LastPrice <= timeCandle.LowPrice)
                    {
                        timeCandle.LowPrice = tick.LastPrice;
                        timeCandle.LowTime = tickTime;
                        timeCandle.LowVolume = tick.LastPrice == timeCandle.LowPrice ?
                            timeCandle.LowVolume + Convert.ToDecimal(tick.Volume) - Convert.ToDecimal(prevTick.Volume) 
                            : Convert.ToDecimal(tick.Volume) - Convert.ToDecimal(prevTick.Volume);
                    }

                    if (tick.LastPrice > prevTick.LastPrice)
                    {
                        timeCandle.UpTicks++;
                    }
                    else if (tick.LastPrice < prevTick.LastPrice)
                    {
                        timeCandle.DownTicks++;
                    }
                    timeCandle.TotalTicks++;
                    
                    //TODO: Money Candle Price level should get updated.
                    UpdatePriceLevel(timeCandle.PriceLevels.ToList(), prevTick.LastPrice, (Convert.ToDecimal(tick.Volume) - Convert.ToDecimal(prevTick.Volume)), tick.LastPrice);

                    timePrevTicks[token] = tick;
                    //timeCandles[token].Add(timeCandle); TODO: The list should get updated automatically

                    timeCandle.TotalVolume += (Convert.ToDecimal(tick.Volume) - Convert.ToDecimal(prevTick.Volume));
                    //TimeSpan timeTraded = tick.Timestamp.Value - (timeCandle.OpenTime + timeFrame);
                    if (tickTime >= (timeCandle.OpenTime + timeFrame))
                    {
                        //Close the candle and trigger event Time Candle Finished
                        timeCandle.State = CandleStates.Finished; //TODO: the list should get updated autmatically
                                                                  //timeCandles[token].Add(timeCandle); 
                        TimeCandleFinished(this, timeCandle);
                        //timer.Restart();
                    }

                }
                
            return TimeFrameCandles[token];
        }


        /// <summary>
        /// This is a short frame of time candle where high/low volume, price levels information, uptick/downticks are not preset.
        /// </summary>
        /// <param name="tick"></param>
        /// <param name="token"></param>
        /// <param name="timeFrame"></param>
        /// <param name="localTicks"></param>
        /// <param name="CandleStartTime"></param>
        /// <returns></returns>
        public List<Candle> StreamingShortTimeFrameCandle(Tick tick, uint token, TimeSpan timeFrame, bool localTicks, DateTime? CandleStartTime = null)
        {
            TimeFrameCandle previousCandle = null;
            TimeFrameCandle timeCandle = null;

            DateTime tickTime = tick.LastTradeTime ?? tick.Timestamp.Value;

            if (TimeFrameCandles.ContainsKey(token))
                {
                    timeCandle = TimeFrameCandles[token].LastOrDefault(x => x.State == CandleStates.Inprogress) as TimeFrameCandle;
                    previousCandle = TimeFrameCandles[token].LastOrDefault(x => x.State == CandleStates.Finished) as TimeFrameCandle;
                }
                if (timeCandle == null || tickTime >= (timeCandle.OpenTime + timeFrame))
                {
                    if (timeCandle != null)
                    {
                        //Close the candle and trigger event Time Candle Finished
                        timeCandle.State = CandleStates.Finished; //TODO: the list should get updated autmatically
                        timeCandle.CloseTime = timeCandle.OpenTime + timeFrame;
                        timeCandle.TotalTicks = timeCandle.DownTicks + timeCandle.UpTicks;
                        TimeCandleFinished(this, timeCandle);
                        previousCandle = timeCandle;
                    }

                    //Create new candle and move the tick there
                    timeCandle = new TimeFrameCandle()
                    {
                        InstrumentToken = token,
                        OpenPrice = tick.LastPrice,
                        OpenTime = (previousCandle == null || previousCandle.CloseTime.Date != tickTime.Date) ? CandleStartTime.HasValue ? CandleStartTime.Value : tickTime : previousCandle.CloseTime, // For testing purpose tick timestamp is better
                        //OpenVolume = timePrevTicks.ContainsKey(token) ? Convert.ToDecimal(tick.Volume) - Convert.ToDecimal(timePrevTicks[token].Volume) : 0,
                        ClosePrice = tick.LastPrice,
                        CloseTime = tickTime,
                        //CloseVolume = timePrevTicks.ContainsKey(token) ? Convert.ToDecimal(tick.Volume) - Convert.ToDecimal(timePrevTicks[token].Volume) : 0,
                        HighPrice = tick.LastPrice,
                        HighTime = tickTime,
                        //HighVolume = timePrevTicks.ContainsKey(token) ? Convert.ToDecimal(tick.Volume) - Convert.ToDecimal(timePrevTicks[token].Volume) : 0,
                        LowPrice = tick.LastPrice,
                        LowTime = tickTime,
                        //LowVolume = timePrevTicks.ContainsKey(token) ? Convert.ToDecimal(tick.Volume) - Convert.ToDecimal(timePrevTicks[token].Volume) : 0,
                        OpenInterest = tick.OI,
                        //DownTicks = previousCandle != null && previousCandle.ClosePrice > tick.LastPrice ? 1 : 0,
                        //UpTicks = previousCandle != null && previousCandle.ClosePrice < tick.LastPrice ? 1 : 0,
                        State = CandleStates.Inprogress,
                        //TotalTicks = 0,
                        TimeFrame = timeFrame,
                        TotalVolume = timePrevTicks.ContainsKey(token) ? Convert.ToDecimal(tick.Volume) - Convert.ToDecimal(timePrevTicks[token].Volume) : 0
                    };

                    //List<CandlePriceLevel> priceLevels = new List<CandlePriceLevel>();
                    //priceLevels.Add(new CandlePriceLevel(tick.LastPrice));
                    //timeCandle.PriceLevels = priceLevels;

                    if (TimeFrameCandles.ContainsKey(token))
                    {
                        TimeFrameCandles[token].Add(timeCandle);
                        timePrevTicks[token] = tick;
                    }
                    else
                    {
                        List<Candle> timeCandleList = new List<Candle>();
                        timeCandleList.Add(timeCandle);
                        TimeFrameCandles.Add(token, timeCandleList);
                        timePrevTicks.Add(token, tick);
                    }

                }

                else if (timeCandle != null)
                {
                    Tick prevTick = timePrevTicks[token];

                    timeCandle.ClosePrice = tick.LastPrice;
                    timeCandle.CloseTime = tickTime;
                    //timeCandle.CloseVolume += (Convert.ToDecimal(tick.Volume) - Convert.ToDecimal(prevTick.Volume));
                    timeCandle.State = CandleStates.Inprogress;
                    //timeCandle.Arg = timer.Elapsed;
                    if (tick.LastPrice >= timeCandle.HighPrice)
                    {
                        timeCandle.HighPrice = tick.LastPrice;
                        timeCandle.HighTime = tickTime;
                        //timeCandle.HighVolume = tick.LastPrice == timeCandle.HighPrice ?
                        //    timeCandle.HighVolume + Convert.ToDecimal(tick.Volume) - Convert.ToDecimal(prevTick.Volume)
                        //    : Convert.ToDecimal(tick.Volume) - Convert.ToDecimal(prevTick.Volume);
                    }
                    if (tick.LastPrice <= timeCandle.LowPrice)
                    {
                        timeCandle.LowPrice = tick.LastPrice;
                        timeCandle.LowTime = tickTime;
                        //timeCandle.LowVolume = tick.LastPrice == timeCandle.LowPrice ?
                        //    timeCandle.LowVolume + Convert.ToDecimal(tick.Volume) - Convert.ToDecimal(prevTick.Volume)
                        //    : Convert.ToDecimal(tick.Volume) - Convert.ToDecimal(prevTick.Volume);
                    }

                    //if (tick.LastPrice > prevTick.LastPrice)
                    //{
                    //    timeCandle.UpTicks++;
                    //}
                    //else if (tick.LastPrice < prevTick.LastPrice)
                    //{
                    //    timeCandle.DownTicks++;
                    //}
                    //timeCandle.TotalTicks++;

                    ////TODO: Money Candle Price level should get updated.
                    //UpdatePriceLevel(timeCandle.PriceLevels.ToList(), prevTick.LastPrice, (Convert.ToDecimal(tick.Volume) - Convert.ToDecimal(prevTick.Volume)), tick.LastPrice);

                    timePrevTicks[token] = tick;
                    //timeCandles[token].Add(timeCandle); TODO: The list should get updated automatically

                    timeCandle.TotalVolume += (Convert.ToDecimal(tick.Volume) - Convert.ToDecimal(prevTick.Volume));
                    //TimeSpan timeTraded = tick.Timestamp.Value - (timeCandle.OpenTime + timeFrame);
                    if (tickTime >= (timeCandle.OpenTime + timeFrame))
                    {
                        //Close the candle and trigger event Time Candle Finished
                        timeCandle.State = CandleStates.Finished; //TODO: the list should get updated autmatically
                                                                  //timeCandles[token].Add(timeCandle); 
                        TimeCandleFinished(this, timeCandle);
                        //timer.Restart();
                    }

                }
                
            //}
            return TimeFrameCandles[token];
        }

        //public List<TimeFrameCandle> StreamingTimeFrameCandle(Tick[] ticks, uint token, TimeSpan timeFrame, bool localTime)
        //{
        //    foreach (Tick tick in ticks)
        //    {
        //        TimeFrameCandle timeCandle = null;

        //        TimeSpan timeTraded = timer.Elapsed; //TimeSpan.Zero;

        //        if (TimeFrameCandles.ContainsKey(token))
        //        {
        //            timeCandle = TimeFrameCandles[token].LastOrDefault();
        //            timeCandle.Arg = timeTraded;
        //            timeTraded -= (TimeSpan) timeCandle.Arg2;
        //        }

        //        //IF timeCandle threshold is reached, trigger an event and call get initial candle set up with initial tick data for open parameters
        //        if (timeTraded > timeFrame || timeCandle == null) //Stop watch
        //        {
        //            if (timeCandle != null)
        //            {
        //                timeCandle.State = CandleStates.Finished; //TODO: the list should get updated autmatically
        //                //timeCandles[token].Add(timeCandle); 
        //                TimeCandleFinished(this, timeCandle);
        //            }
        //            timer.Restart();
        //            timeCandle = new TimeFrameCandle()
        //            {
        //                Arg2 = timer.Elapsed,
        //                Arg = timer.Elapsed,
        //                OpenPrice = tick.LastPrice,
        //                OpenTime = tick.Timestamp.Value,
        //                OpenVolume = tick.Volume,
        //                ClosePrice = tick.LastPrice,
        //                CloseTime = tick.Timestamp.Value,
        //                CloseVolume = tick.Volume,
        //                HighPrice = tick.LastPrice,
        //                HighTime = tick.Timestamp.Value,
        //                HighVolume = tick.Volume,
        //                LowPrice = tick.LastPrice,
        //                LowTime = tick.Timestamp.Value,
        //                LowVolume = tick.Volume,
        //                OpenInterest = tick.OI,
        //                DownTicks = 0,
        //                UpTicks = 0,
        //                State = CandleStates.Inprogress,
        //                TotalTicks = 0,
        //                TimeFrame = timeFrame
        //            };

        //            List<CandlePriceLevel> priceLevels = new List<CandlePriceLevel>();
        //            priceLevels.Add(new CandlePriceLevel(tick.LastPrice));
        //            timeCandle.PriceLevels = priceLevels;

        //            if (TimeFrameCandles.ContainsKey(token))
        //            {
        //                TimeFrameCandles[token].Add(timeCandle);
        //                timePrevTicks[token] = tick;
        //            }
        //            else
        //            {
        //                List<TimeFrameCandle> timeCandleList = new List<TimeFrameCandle>();
        //                timeCandleList.Add(timeCandle);
        //                TimeFrameCandles.Add(token, timeCandleList);
        //                timePrevTicks.Add(token, tick);
        //            }

        //            //MarketTicks[token].Add(tick);
        //        }
        //        else
        //        {
        //            timeCandle.ClosePrice = tick.LastPrice;
        //            timeCandle.CloseTime = tick.Timestamp.Value;
        //            timeCandle.CloseVolume = tick.Volume;
        //            timeCandle.State = CandleStates.Inprogress;
        //            //timeCandle.Arg = timer.Elapsed;
        //            if (tick.LastPrice >= timeCandle.HighPrice)
        //            {
        //                timeCandle.HighPrice = tick.LastPrice;
        //                timeCandle.HighTime = tick.Timestamp.Value;
        //                timeCandle.HighVolume = tick.LastPrice == timeCandle.HighPrice ? timeCandle.HighVolume + tick.Volume : tick.Volume;
        //            }
        //            if (tick.LastPrice <= timeCandle.LowPrice)
        //            {
        //                timeCandle.LowPrice = tick.LastPrice;
        //                timeCandle.LowTime = tick.Timestamp.Value;
        //                timeCandle.LowVolume = tick.LastPrice == timeCandle.LowPrice ? timeCandle.LowVolume + tick.Volume : tick.Volume;
        //            }

        //            Tick prevTick = timePrevTicks[token];
        //            if (tick.LastPrice > prevTick.LastPrice) timeCandle.UpTicks++; else timeCandle.DownTicks++;

        //            //TODO: Money Candle Price level should get updated.
        //            UpdatePriceLevel(timeCandle.PriceLevels.ToList(), prevTick.LastPrice, prevTick.Volume, uptrend: tick.LastPrice > prevTick.LastPrice);

        //            timePrevTicks[token] = tick;
        //            //timeCandles[token].Add(timeCandle); TODO: The list should get updated automatically
        //        }
        //    }
        //    return TimeFrameCandles[token];
        //}

        /// <summary>
        /// Candle 
        /// </summary>
        /// <param name="ticks"></param>
        /// <param name="token"></param>
        /// <param name="volumeThreshold"></param>
        /// <returns></returns>
        public List<Candle> StreamingVolumeCandle(Tick tick, uint token, uint volumeThreshold)
        {
            VolumeCandle previousCandle = null;
            //foreach (Tick tick in ticks)
            //{
                DateTime tickTime = tick.LastTradeTime ?? tick.Timestamp.Value;
                VolumeCandle volumeCandle = null;
                decimal volumeTraded = 0;
                decimal cumVolumeTraded = 0;
               

                if (VolumeCandles.ContainsKey(token))
                {
                    volumeCandle = VolumeCandles[token].LastOrDefault(x => x.State == CandleStates.Inprogress) as VolumeCandle;
                    
                    volumeTraded = Math.Max(Convert.ToDecimal(tick.Volume) - Convert.ToDecimal(volumePrevTicks[token].Volume), 0);
                    cumVolumeTraded = volumeCandle.Volume + volumeTraded;
                }

                //IF volumeCandle threshold is reached, trigger an event and call get initial candle set up with initial tick data for open parameters
                if (cumVolumeTraded >= volumeThreshold || volumeCandle == null)
                {
                    if (volumeCandle != null)
                    {
                        volumeCandle.State = CandleStates.Finished;
                        //VolumeCandles[token].Add(volumeCandle); //TODO: the list should get updated autmatically
                        //volumeCandle.Volume = cumVolumeTraded;
                        volumeCandle.CloseTime = tickTime;
                        volumeCandle.TotalTicks = volumeCandle.DownTicks + volumeCandle.UpTicks;
                        //timeCandles[token].Add(timeCandle); 
                        //VolumeCandleFinished(this, volumeCandle);
                        //timer.Restart();
                        previousCandle = volumeCandle;
                        VolumeCandleFinished(this, volumeCandle);
                    }

                    volumeCandle = new VolumeCandle()
                    {
                        Volume = volumeTraded,// tick.Volume,
                        TotalVolume = volumeTraded,
                        InstrumentToken = token,
                        //Volume = volumeThreshold,
                        OpenPrice = tick.LastPrice,
                        OpenTime = previousCandle == null || previousCandle.CloseTime.Date != tickTime.Date ? tickTime : previousCandle.CloseTime,
                        OpenVolume = volumeTraded,
                        ClosePrice = tick.LastPrice,
                        CloseTime = tickTime,
                        CloseVolume = volumeTraded,
                        HighPrice = tick.LastPrice,
                        HighTime = tickTime,
                        HighVolume = volumeTraded,
                        LowPrice = tick.LastPrice,
                        LowTime = tickTime,
                        LowVolume = volumeTraded,
                        OpenInterest = tick.OI,
                        //DownTicks = volumePrevTicks[token].LastPrice > tick.LastPrice ? 1 : 0,
                        //UpTicks = volumePrevTicks[token].LastPrice < tick.LastPrice ? 1 : 0,
                        State = CandleStates.Inprogress,
                        //TotalTicks = 0
                    };
                    //volumeTraded = tick.Volume - -previousTick.Volume;

                    List<CandlePriceLevel> priceLevels = new List<CandlePriceLevel>();
                    priceLevels.Add(new CandlePriceLevel(tick.LastPrice));
                    volumeCandle.PriceLevels = priceLevels;

                    if (VolumeCandles.ContainsKey(token))
                    {
                        VolumeCandles[token].Add(volumeCandle);
                        volumePrevTicks[token] = tick;
                    }
                    else
                    {
                        List<Candle> volumeCandleList = new List<Candle>();
                        volumeCandleList.Add(volumeCandle);
                        VolumeCandles.Add(token, volumeCandleList);
                        volumePrevTicks.Add(token, tick);
                    }

                    volumeCandle.DownTicks = volumePrevTicks[token].LastPrice > tick.LastPrice ? 1 : 0;
                    volumeCandle.UpTicks = volumePrevTicks[token].LastPrice < tick.LastPrice ? 1 : 0;
                    volumeCandle.TotalTicks = volumeCandle.UpTicks + volumeCandle.DownTicks;
                    //MarketTicks[token].Add(tick);
                }
                else
                {
                    volumeCandle.ClosePrice = tick.LastPrice;
                    volumeCandle.CloseTime = tickTime;
                    volumeCandle.CloseVolume = volumeTraded;
                    volumeCandle.State = CandleStates.Inprogress;
                    volumeCandle.Volume += (volumeTraded);
                volumeCandle.TotalVolume += (volumeTraded);

                if (tick.LastPrice >= volumeCandle.HighPrice)
                    {
                        volumeCandle.HighPrice = tick.LastPrice;
                        volumeCandle.HighTime = tickTime;
                        volumeCandle.HighVolume = tick.LastPrice == volumeCandle.HighPrice ? volumeCandle.HighVolume + volumeTraded : volumeTraded;
                    }
                    if (tick.LastPrice <= volumeCandle.LowPrice)
                    {
                        volumeCandle.LowPrice = tick.LastPrice;
                        volumeCandle.LowTime = tickTime;
                        volumeCandle.LowVolume = tick.LastPrice == volumeCandle.LowPrice ? volumeCandle.LowVolume + volumeTraded : volumeTraded;
                    }

                    Tick prevTick = volumePrevTicks[token];
                    if (tick.LastPrice > prevTick.LastPrice)
                    {
                        volumeCandle.UpTicks++;
                    }
                    else if (tick.LastPrice < prevTick.LastPrice)
                    {
                        volumeCandle.DownTicks++;
                    }
                    volumeCandle.TotalTicks++;

                    //TODO: Money Candle Price level should get updated.
                    UpdatePriceLevel(volumeCandle.PriceLevels.ToList(), prevTick.LastPrice, volumeTraded, tick.LastPrice);

                    volumePrevTicks[token] = tick;
                    //VolumeCandles[token].Add(volumeCandle); TODO: The list should get updated automatically
                }
           // }
            return VolumeCandles[token];
        }

        public List<Candle> StreamingMoneyCandle (Tick tick, uint token, decimal moneySize)
        {
            try
            {
                MoneyCandle previousCandle = null;
                //foreach (Tick tick in ticks)
                //{
                    DateTime tickTime = tick.LastTradeTime ?? tick.Timestamp.Value;
                    //Tick prevTick;
                    //Calculate money dollar exchanged.
                    MoneyCandle moneyCandle = null;
                    decimal moneyTraded = 0;
                    decimal cumMoneyTraded = 0;
                    decimal volumeTraded = 0;

                    if (MoneyCandles.ContainsKey(token))
                    {
                        moneyCandle = MoneyCandles[token].LastOrDefault(x => x.State == CandleStates.Inprogress) as MoneyCandle;
                        //moneyTraded = (decimal)moneyCandle.Arg;

                        volumeTraded = Math.Max(Convert.ToDecimal(tick.Volume) - Convert.ToDecimal(moneyPrevTicks[token].Volume), 0);

                        moneyTraded = volumeTraded * moneyPrevTicks[token].LastPrice;
                        cumMoneyTraded = moneyCandle.Money + moneyTraded;

                    }

                    //IF moneyCandle threshold is reached, trigger an event and call get initial candle set up with initial tick data for open parameters
                    if (cumMoneyTraded >= moneySize || moneyCandle == null)
                    {
                        if (moneyCandle != null)
                        {
                            moneyCandle.State = CandleStates.Finished;
                        //MoneyCandles[token].Add(moneyCandle); //TODO: the list should get updated autmatically
                        moneyCandle.TotalVolume = volumeTraded;
                            moneyCandle.CloseTime = tickTime;
                            moneyCandle.TotalTicks = moneyCandle.DownTicks + moneyCandle.UpTicks;
                            previousCandle = moneyCandle;
                            MoneyCandleFinished(this, moneyCandle);
                        }

                        //MoneyThresholdReached?.Invoke(this, firsttick);
                        moneyCandle = new MoneyCandle()
                        {
                            Money = moneyTraded,
                            InstrumentToken = token,
                            OpenPrice = tick.LastPrice,
                            OpenTime = previousCandle == null || previousCandle.CloseTime.Date != tickTime.Date ? tickTime : previousCandle.CloseTime,
                            OpenVolume = volumeTraded,
                            ClosePrice = tick.LastPrice,
                            CloseTime = tickTime,
                            CloseVolume = volumeTraded,
                            HighPrice = tick.LastPrice,
                            HighTime = tickTime,
                            HighVolume = volumeTraded,
                            LowPrice = tick.LastPrice,
                            LowTime = tickTime,
                            LowVolume = volumeTraded,
                            OpenInterest = tick.OI,
                            State = CandleStates.Inprogress
                        };
                        //moneyTraded = tick.LastPrice * tick.Volume;

                        List<CandlePriceLevel> priceLevels = new List<CandlePriceLevel>();
                        priceLevels.Add(new CandlePriceLevel(tick.LastPrice));
                        moneyCandle.PriceLevels = priceLevels;


                        //moneyCandle.UpTicks = moneyCandle.DownTicks = 0;
                        //moneyCandle.State = CandleStates.Inprogress;
                        if (MoneyCandles.ContainsKey(token))
                        {
                            MoneyCandles[token].Add(moneyCandle);
                            moneyPrevTicks[token] = tick;
                        }
                        else
                        {
                            List<Candle> moneyCandles = new List<Candle>();
                            moneyCandles.Add(moneyCandle);
                            MoneyCandles.Add(token, moneyCandles);
                            moneyPrevTicks.Add(token, tick);
                        }
                        moneyCandle.DownTicks = moneyPrevTicks[token].LastPrice > tick.LastPrice ? 1 : 0;
                        moneyCandle.UpTicks = moneyPrevTicks[token].LastPrice < tick.LastPrice ? 1 : 0;
                        moneyCandle.TotalTicks = moneyCandle.UpTicks + moneyCandle.DownTicks;
                        //MarketTicks[token].Add(tick);
                    }
                    else
                    {
                        moneyCandle.ClosePrice = tick.LastPrice;
                        moneyCandle.CloseTime = tickTime;
                        moneyCandle.CloseVolume = volumeTraded;
                        moneyCandle.Money += moneyTraded; // + tick.LastPrice * tick.Volume;
                        moneyCandle.State = CandleStates.Inprogress;

                        if (tick.LastPrice >= moneyCandle.HighPrice)
                        {
                            moneyCandle.HighPrice = tick.LastPrice;
                            moneyCandle.HighTime = tickTime;
                            moneyCandle.HighVolume = tick.LastPrice == moneyCandle.HighPrice ? moneyCandle.HighVolume + volumeTraded : volumeTraded;
                        }
                        if (tick.LastPrice <= moneyCandle.LowPrice)
                        {
                            moneyCandle.LowPrice = tick.LastPrice;
                            moneyCandle.LowTime = tickTime;
                            moneyCandle.LowVolume = tick.LastPrice == moneyCandle.LowPrice ? moneyCandle.LowVolume + volumeTraded : volumeTraded;

                        }
                        Tick prevTick = moneyPrevTicks[token];
                        if (tick.LastPrice > prevTick.LastPrice)
                        {
                            moneyCandle.UpTicks++;
                        }
                        else if (tick.LastPrice < prevTick.LastPrice)
                        {
                            moneyCandle.DownTicks++;
                        }
                        moneyCandle.TotalTicks++;
                        //TODO: Money Candle Price level should get updated.
                        UpdatePriceLevel(moneyCandle.PriceLevels.ToList(), prevTick.LastPrice, volumeTraded, tick.LastPrice);

                        moneyPrevTicks[token] = tick;
                        //MoneyCandles[token].Add(moneyCandle); TODO: The list should get updated automatically
                    }
               // }
            }
            catch (Exception ex)
            {
                Logger.LogWrite(ex.Message + ex.StackTrace);
                Logger.LogWrite("Trading Stopped as algo encountered an error");
            }
            return MoneyCandles[token];
        }

        private void UpdatePriceLevel(List<CandlePriceLevel> priceLevels, decimal price, decimal volume, decimal currentTickPrice)
        {
            CandlePriceLevel currentPriveLevel = priceLevels.FirstOrDefault(x => x.Price == price);

            if (currentPriveLevel == null)
            {
                currentPriveLevel = new CandlePriceLevel(price);
                priceLevels.Add(currentPriveLevel);
            }

            if (currentTickPrice > price)
            {
                currentPriveLevel.BuyCount++;
                currentPriveLevel.BuyVolume += volume;
            }
            else if(currentTickPrice < price)
            {
                currentPriveLevel.SellCount++;
                currentPriveLevel.SellVolume += volume;
            }

            currentPriveLevel.TotalVolume += volume;
        }
        //private void OnVolumeThresholdReached(object sender, Tuple<UInt32, List<Tick>> token_ticks)
        //{
        //    uint token = token_ticks.Item1;
        //    List<Tick> ticks = token_ticks.Item2;

        //    Tick lasttick = ticks.Last();
        //    Tick firsttick = ticks.First();

        //    VolumeCandle volumeCandle = new VolumeCandle()
        //    {
        //        Arg = _volumeLimit,
        //        ClosePrice = lasttick.Close,
        //        CloseTime = lasttick.Timestamp.Value,
        //        CloseVolume = lasttick.Volume,
        //        OpenPrice = firsttick.Close,
        //        OpenTime = firsttick.Timestamp.Value,
        //        OpenVolume = firsttick.Volume,
        //        OpenInterest = lasttick.OI
        //    };

        //    decimal maxPrice = ticks[0].LastPrice, minPrice = ticks[0].LastPrice;
        //    decimal maxVolume = ticks[0].Volume, priceatMaxVol = ticks[0].LastPrice;
        //    uint volatMaxPrice = ticks[0].Volume, volatMinPrice = ticks[0].Volume;
        //    uint totalVolume = ticks[0].Volume;
        //    DateTime? timeatMaxPrice = ticks[0].Timestamp, timeatMinPrice = ticks[0].Timestamp;
        //    int upTicks = 0;
        //    int downTicks = 0;
        //    uint oi = ticks[0].OI;
        //    Tick prevTick;
        //    decimal currentPrice = ticks[0].LastPrice;
        //    decimal prevPrice = ticks[0].LastPrice;

        //    CandlePriceLevel priceLevel = new CandlePriceLevel(prevPrice);
        //    CandlePriceLevel MaxPriceLevel = new CandlePriceLevel(maxPrice);
        //    CandlePriceLevel MinPriceLevel = new CandlePriceLevel(minPrice);
        //    List<CandlePriceLevel> priceLevels = new List<CandlePriceLevel>();

        //    priceLevels.Add(priceLevel);

        //    for (int i = 1; i < ticks.Count; i++)
        //    {
        //        Tick tick = ticks[i];
        //        prevTick = ticks[i - 1];
        //        currentPrice = tick.LastPrice;
        //        totalVolume += tick.Volume;
        //        if (tick.LastPrice >= maxPrice)
        //        {
        //            maxPrice = tick.LastPrice;
        //            timeatMaxPrice = tick.Timestamp;
        //            volatMaxPrice = tick.LastPrice == maxPrice ? volatMaxPrice + tick.Volume : tick.Volume;
        //        }
        //        if (tick.LastPrice <= minPrice)
        //        {
        //            minPrice = tick.LastPrice;
        //            timeatMinPrice = tick.Timestamp;
        //            volatMinPrice = tick.LastPrice == minPrice ? volatMinPrice + tick.Volume : tick.Volume;
        //        }

        //        UpdatePriceLevel(priceLevels, prevPrice, prevTick.Volume, uptrend: currentPrice > prevPrice);
        //        if (currentPrice > prevPrice) upTicks++; else downTicks++;
        //    }

        //    volumeCandle.DownTicks = downTicks;
        //    volumeCandle.UpTicks = upTicks;
        //    volumeCandle.HighPrice = maxPrice;
        //    volumeCandle.HighTime = timeatMaxPrice.Value;
        //    volumeCandle.HighVolume = volatMaxPrice;

        //    volumeCandle.LowPrice = minPrice;
        //    volumeCandle.LowTime = timeatMaxPrice.Value;
        //    volumeCandle.LowVolume = volatMinPrice;

        //    volumeCandle.PriceLevels = priceLevels;
        //    volumeCandle.State = CandleStates.Finished;

        //    volumeCandle.TotalTicks = upTicks + downTicks;
        //    volumeCandle.TotalVolume = totalVolume;

        //    //send these to subscribers
        //}

        //private void OnMoneyThresholdReached(object sender, Tuple<UInt32, List<Tick>> token_ticks)
        //{
        //    uint token = token_ticks.Item1;
        //    List<Tick> ticks = token_ticks.Item2;

        //    Tick lasttick = ticks.Last();
        //    Tick firsttick = ticks.First();

        //    MoneyCandle moneyCandle = new MoneyCandle()
        //    {
        //        Arg = _moneyLimit,
        //        ClosePrice = lasttick.Close,
        //        CloseTime = lasttick.Timestamp.Value,
        //        CloseVolume = lasttick.Volume,
        //        OpenPrice = firsttick.Close,
        //        OpenTime = firsttick.Timestamp.Value,
        //        OpenVolume = firsttick.Volume,
        //        OpenInterest = lasttick.OI
        //    };

        //    decimal maxPrice, minPrice, maxMoney, minMoney, totalMoney, priceatMaxMoney, priceatMinMoney;
        //    maxPrice = minPrice = maxMoney = minMoney = totalMoney = ticks[0].LastPrice * ticks[0].Volume;
        //    priceatMaxMoney = priceatMinMoney = ticks[0].LastPrice;

        //    DateTime? timeatMaxMoney = ticks[0].Timestamp, timeatMinMoney = ticks[0].Timestamp;
        //    int upTicks = 0;
        //    int downTicks = 0;
        //    uint oi = ticks[0].OI;
        //    Tick prevTick;
        //    decimal currentPrice = ticks[0].LastPrice;
        //    decimal prevPrice = ticks[0].LastPrice;

        //    CandlePriceLevel priceLevel = new CandlePriceLevel(prevPrice);
        //    CandlePriceLevel MaxMoneyLevel = new CandlePriceLevel(priceatMaxMoney);
        //    CandlePriceLevel MinMoneyLevel = new CandlePriceLevel(priceatMinMoney);
        //    List<CandlePriceLevel> priceLevels = new List<CandlePriceLevel>();

        //    priceLevels.Add(priceLevel);

        //    for (int i = 1; i < ticks.Count; i++)
        //    {
        //        Tick tick = ticks[i];
        //        prevTick = ticks[i - 1];
        //        currentPrice = tick.LastPrice;
        //        prevPrice = prevTick.LastPrice;
        //        //totalVolume += tick.Volume;

        //        //if (tick.LastPrice == priceatMaxMoney)
        //        //{
        //        //    maxMoney += tick.LastPrice * tick.Volume;
        //        //}
        //        //if (tick.LastPrice*tick.Volume >= maxMoney)
        //        //{
        //        //    maxMoney = tick.LastPrice*tick.Volume;
        //        //    timeatMaxMoney = tick.Timestamp;
        //        //    priceatMaxMoney = tick.LastPrice;  // == maxPrice ? volatMaxPrice + tick.Volume : tick.Volume;
        //        //}
        //        //if (tick.LastPrice <= minPrice)
        //        //{
        //        //    minPrice = tick.LastPrice;
        //        //    timeatMinPrice = tick.Timestamp;
        //        //    volatMinPrice = tick.LastPrice == minPrice ? volatMinPrice + tick.Volume : tick.Volume;
        //        //}

        //        UpdatePriceLevel(priceLevels, prevPrice, prevTick.Volume, uptrend: currentPrice > prevPrice);
        //        if (currentPrice > prevPrice) upTicks++; else downTicks++;
        //    }

        //    moneyCandle.DownTicks = downTicks;
        //    moneyCandle.UpTicks = upTicks;
        //    moneyCandle.HighPrice = priceLevels.Max(x=>x.Price*(x.BuyVolume - x.SellVolume));
        //    //moneyCandle.HighTime = timeatMaxPrice.Value;
        //    //moneyCandle.HighVolume = volatMaxPrice;

        //    moneyCandle.LowPrice =  priceLevels.Min(x => x.Price * (x.BuyVolume - x.SellVolume));
        //    //moneyCandle.LowTime = timeatMaxPrice.Value;
        //    //moneyCandle.LowVolume = volatMinPrice;

        //    moneyCandle.PriceLevels = priceLevels;
        //    moneyCandle.State = CandleStates.Finished;
        //    moneyCandle.TotalTicks = upTicks + downTicks;
        //    //moneyCandle.TotalMoney = totalVolume;

        //    //send these to subscribers
        //}



        //private void InputTickStream(Tick[] ticks, decimal moneyThreshold)
        //{
        //    foreach (Tick Tickdata in ticks)
        //    {
        //        if (MarketTicks.ContainsKey(Tickdata.InstrumentToken))
        //        {
        //            MarketTicks[Tickdata.InstrumentToken].Add(Tickdata);
        //        }
        //        else
        //        {
        //            List<Tick> tickList = new List<Tick>();
        //            tickList.Add(Tickdata);
        //            MarketTicks.Add(Tickdata.InstrumentToken, tickList);
        //        }

        //        if (MarketTicks[Tickdata.InstrumentToken].Sum(x => x.Volume) > _volumeLimit)
        //        {
        //            VolumeThresholdReached?.Invoke(this, new Tuple<UInt32, List<Tick>>(Tickdata.InstrumentToken, MarketTicks[Tickdata.InstrumentToken]));
        //        }
        //        if (MarketTicks[Tickdata.InstrumentToken].Sum(x => x.LastPrice) > _moneyLimit)
        //        {
        //            MoneyThresholdReached?.Invoke(this, new Tuple<UInt32, List<Tick>>(Tickdata.InstrumentToken, MarketTicks[Tickdata.InstrumentToken]));
        //        }


        //    }
        //}
    }
}
