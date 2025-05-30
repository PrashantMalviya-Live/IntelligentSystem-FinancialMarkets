﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using GlobalLayer;
using Algorithms.Utils;
using System.Diagnostics.Eventing.Reader;
using DBAccess;
namespace DataAccess
{
    public class Storage //: IZMQ //, IObserver<Tick[]>
    {
        public static Dictionary<UInt32, Queue<Tick>> LiveTickData;
        public static Queue<Tick> LiveTicks;
        public IDisposable UnsubscriptionToken;
        //DataAccess.QuestDB ds;
        //InfluxDB ds;
        public readonly TimeSpan MARKET_START_TIME = new TimeSpan(9, 15, 0);
        Dictionary<uint, List<Candle>> TimeCandles;
        CandleManger candleManger;
        TimeSpan _candleTimeSpan;
        //SQlDAO dao;

        private readonly ITimeStreamDAO _idAO;


        //public virtual void Subscribe(Publisher publisher)
        //{
        //    UnsubscriptionToken = publisher.Subscribe(this);
        //}
        public Storage(bool storeTicks, bool storeCandles, ITimeStreamDAO dAO)
        {
            _idAO = dAO;
#if !BACKTEST
            if (storeTicks)
            {
                //ds = new DataAccess.QuestDB();
                //ds = new InfluxDB();
                LiveTicks = new Queue<Tick>();
                //if(sqlStorage)
                InitTimer();
            }
            if (storeCandles)
            {
                //dao = new SQlDAO();
                _candleTimeSpan = new TimeSpan(0, 1, 0);
                TimeCandles = new Dictionary<uint, List<Candle>>();
                candleManger = new CandleManger(TimeCandles, CandleType.Time);
                candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;
            }
#endif

        }

        


        //public virtual void Subscribe(Ticker publisher)
        //{
        //    UnsubscriptionToken = publisher.Subscribe(this);

        //    LiveTicks = new Queue<Tick>();

        //    InitTimer();
        //}
        //public void ZMQClient()
        //{
        //    using (var context = new ZContext())
        //    using (var subscriber = new ZSocket(context, ZSocketType.SUB))
        //    {
        //        subscriber.Connect("tcp://127.0.0.1:5555");
        //        subscriber.Subscribe("");
        //        while (true)
        //        {
        //            using (ZMessage message = subscriber.ReceiveMessage())
        //            {
        //                // Read envelope with address
        //                byte[] tickData = message[0].Read();
        //                Tick[] ticks = TickDataSchema.ParseTicks(tickData);
        //                OnNext(ticks);
        //            }
        //        }
        //    }
        //}

        //public virtual void Unsubscribe()
        //{
        //    UnsubscriptionToken.Dispose();
        //}
        public virtual void OnError(Exception ex)
        {
            ///TODO: Log the error. Also handle the error.
        }

        public virtual void OnCompleted()
        {
        }

        //make sure ref is working with struct . else make it class
        public void StoreTicks(List<Tick> ticks, bool shortenedTicks)
        {
            if (LiveTicks == null)
            {
                return;
            }
            lock (LiveTicks)
            {
                foreach (Tick Tickdata in ticks)
                {
                    //ds.WriteTick(Tickdata);
                    if (shortenedTicks)
                        ShortenTheTick(Tickdata);
                    LiveTicks.Enqueue(Tickdata);
                   // ds.InsertTick(Tickdata);
                }
            }
            return;
        }
        public static void ShortenTheTick(Tick tick)
        {
            tick.AveragePrice = 0;
            tick.Bids = null;
            tick.BuyQuantity = 0;
            tick.Change = 0;
            tick.Close = 0;
            tick.Open = 0;
            tick.High = 0;
            tick.Low = 0;
            tick.Offers = null;
            tick.OIDayHigh = 0;
            tick.OIDayLow = 0;
            tick.SellQuantity = 0;
            tick.LastQuantity = 0;

        }
        /// <summary>
        /// Store raw ticks
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        async Task StoreRawTicks(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (LiveTicks == null || LiveTicks.Count == 0)
            {
                return;
            }

            Queue<Tick> localCopyTicks;
            lock (LiveTicks)
            {
                localCopyTicks = new Queue<Tick>(LiveTicks);
                LiveTicks.Clear();
            }

            //DataAccess.SQlDAO dao = new SQlDAO();
            //dao.StoreTickData(localCopyTicks);

            // AWSTimestreamdb storage = new AWSTimestreamdb();
            //await storage.WriteTicksAsync(localCopyTicks);

            await _idAO.WriteTicksAsync(localCopyTicks);
        }

        public void StoreTimeCandles(List<Tick> ticks)
        {
            foreach (Tick tick in ticks)
            {
                uint token = tick.InstrumentToken;

                //Check the below statement, this should not keep on adding to 
                //TimeCandles with everycall, as the list doesnt return new candles unless built

                if (TimeCandles.ContainsKey(token))
                {
                    candleManger.StreamingTimeFrameCandle(tick, token, _candleTimeSpan, true); // TODO: USING LOCAL VERSION RIGHT NOW

                    //sTokenEMA[token].Process(TimeCandles[token].Last().ClosePrice, isFinal: false);
                    //lTokenEMA[token].Process(tick.LastPrice, isFinal: false);
                }
                else
                {
                    DateTime lastCandleEndTime;
                    DateTime? tickTime = tick.LastTradeTime ?? tick.Timestamp;
                    DateTime? candleStartTime = CheckCandleStartTime(tickTime.Value, out lastCandleEndTime);

                    if (candleStartTime.HasValue)
                    {
                        //candle starts from there
                        candleManger.StreamingTimeFrameCandle(tick, token, _candleTimeSpan, true, candleStartTime); // TODO: USING LOCAL VERSION

                    }
                }
            }
        }
       
        private void CandleManger_TimeCandleFinished(object sender, Candle e)
        {
            SaveCandle(e);

            //Limit size of TimeCandles in the memory
            if(TimeCandles.ContainsKey(e.InstrumentToken) && TimeCandles[e.InstrumentToken].Count > 1)
            {
                TimeCandles[e.InstrumentToken].Remove(TimeCandles[e.InstrumentToken][0]);
            }
        }
        public void SaveCandle(Candle candle)
        {
            CandlePriceLevel maxPriceLevel = candle.MaxPriceLevel;
            CandlePriceLevel minPriceLevel = candle.MinPriceLevel;
            IEnumerable<CandlePriceLevel> priceLevels = candle.PriceLevels;

            int candleId = _idAO.SaveCandle(candle.Arg, candle.ClosePrice, candle.CloseTime, candle.CloseVolume, candle.DownTicks,
                candle.HighPrice, candle.HighTime, candle.HighVolume, candle.InstrumentToken,
                candle.LowPrice, candle.LowTime, candle.LowVolume,
                maxPriceLevel.BuyCount, maxPriceLevel.BuyVolume, maxPriceLevel.Money, maxPriceLevel.Price,
                maxPriceLevel.SellCount, maxPriceLevel.SellVolume, maxPriceLevel.TotalVolume,
                minPriceLevel.BuyCount, minPriceLevel.BuyVolume, minPriceLevel.Money, minPriceLevel.Price,
                minPriceLevel.SellCount, minPriceLevel.SellVolume, minPriceLevel.TotalVolume,
                candle.OpenInterest, candle.OpenPrice, candle.OpenTime, candle.OpenVolume,
                candle.RelativeVolume, candle.State, candle.TotalPrice, candle.TotalTicks, candle.TotalVolume, candle.UpTicks, candle.CandleType);

            foreach (CandlePriceLevel candlePriceLevel in candle.PriceLevels)
            {
                _idAO.SaveCandlePriceLevels(candleId, candlePriceLevel.BuyCount, candlePriceLevel.BuyVolume, candlePriceLevel.Money, candlePriceLevel.Price,
                candlePriceLevel.SellCount, candlePriceLevel.SellVolume, candlePriceLevel.TotalVolume);
            }
        }
        private DateTime? CheckCandleStartTime(DateTime currentTime, out DateTime lastEndTime)
        {
            double mselapsed = (currentTime.TimeOfDay - MARKET_START_TIME).TotalMilliseconds % _candleTimeSpan.TotalMilliseconds;
            DateTime? candleStartTime = null;
            
            //Market hasn't started yet
            if (currentTime.TimeOfDay < MARKET_START_TIME) //less than a second
            {
                lastEndTime = System.DateTime.Now;
                return null;
            }
            if (mselapsed < 60 * 1000)
            {
                candleStartTime = currentTime.Date.Add(TimeSpan.FromMilliseconds(currentTime.TimeOfDay.TotalMilliseconds - mselapsed));
            }
            //else
            //{
            lastEndTime = currentTime.Date.Add(TimeSpan.FromMilliseconds(currentTime.TimeOfDay.TotalMilliseconds - mselapsed));
            //}

            return candleStartTime;
        }
        public void InitTimer()
        {
#if !BACKTEST
            GlobalObjects.OHLCTimer = new System.Timers.Timer(20000);
            GlobalObjects.OHLCTimer.Start();

            GlobalObjects.OHLCTimer.Elapsed += async (sender, e) => { await StoreRawTicks(sender, e); };
            //GlobalObjects.OHLCTimer.Elapsed += StoreRawTicks;
#endif
        }

    }
}
