using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
//using Pub_Sub;
using KiteConnect;
using GlobalLayer;
using DataAccess;
using ZMQFacade;
//using ZeroMQ;

namespace Algos.TLogics
{
    public class Storage1 : IZMQ //, IObserver<Tick[]>
    {
        public static Dictionary<UInt32, Queue<Tick>> LiveTickData;
        public static Queue<Tick> LiveTicks;
        public IDisposable UnsubscriptionToken;
        static bool _sqlStorage = true;
        //public virtual void Subscribe(Publisher publisher)
        //{
        //    UnsubscriptionToken = publisher.Subscribe(this);
        //}
        public Storage1(bool sqlStorage = true)
        {
            LiveTicks = new Queue<Tick>();

            _sqlStorage = sqlStorage;
           //if(sqlStorage)
                InitTimer();

           // InitNCacheStorage();
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
        public virtual void OnNext(Tick tick)
        {
            if (LiveTicks == null)
            {
                return ;
            }

            List<KeyValuePair<uint, Tick>> tickCollection = new List<KeyValuePair<uint, Tick>>();
            lock (LiveTicks)
            {
                //foreach (Tick Tickdata in ticks)
                //{
                    LiveTicks.Enqueue(tick);
                    KeyValuePair<uint, Tick> keyValue = new KeyValuePair<uint, Tick>(tick.InstrumentToken, tick);
                    tickCollection.Add(keyValue);
                //}
            }
            //IgniteConnector.Put(tickCollection);
            //t1.Start(ticks);
            //NCacheFacade.UpdateNCacheData(ticks);

            return ;
        }

        /// <summary>
        /// Store raw ticks
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void StoreRawTicks(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (LiveTicks == null || LiveTicks.Count == 0)
            {
                return;
            }

            ///TODO: Implement logic to either store or process each tick. KAFKA!!
            Queue<Tick> localCopyTicks;
            lock (LiveTicks)
            {
                localCopyTicks = new Queue<Tick>(LiveTicks);
                LiveTicks.Clear();
            }

            if (_sqlStorage)
            {
                DataAccess.MarketDAO dao = new MarketDAO();
                dao.StoreTickData(localCopyTicks);
            }

        }

        public void StopTrade(bool stop)
        {
            //_stopTrade = stop;
        }
        public static void InitTimer()
        {
            GlobalObjects.OHLCTimer = new System.Timers.Timer(20000);
            GlobalObjects.OHLCTimer.Start();

            GlobalObjects.OHLCTimer.Elapsed += StoreRawTicks;
        }

    }
}
