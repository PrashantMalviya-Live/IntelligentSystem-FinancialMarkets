using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;


namespace GlobalLayer
{
    public static class GlobalObjects
    {
        public static System.Timers.Timer OHLCTimer;

        public static void InitBoxTimer()
        {
            OHLCTimer = new System.Timers.Timer(60000);
            OHLCTimer.Start();
        }

       

        //private static IIgnite _ignite;
        //private static IIgniteClient _igniteClient;
        //private static IDataStreamer<TickKey, Tick> _idataStreamer;
        //private static IMessaging _imessaging;
        //private static ICache<TickKey, Tick> _icache;

        //private static ICache<uint, Tick> _imarketDataCache;
        //private static IDataStreamer<uint, Tick> _imarketDataStreamer;

        //private static ICacheClient<TickKey, Tick> _icacheClient;
        //private static IContinuousQueryHandle _iContinuousQuery;

        //public static IIgnite Ignite
        //{
        //    get
        //    {
        //            return _ignite;

        //    }
        //    set
        //    {
        //        try
        //        {
        //            _ignite = value;
        //        }
        //        catch (Exception ex)
        //        {
        //            throw ex;
        //        }
        //    }
        //}
        //public static IIgniteClient IgniteClient
        //{
        //    get
        //    {
        //        return _igniteClient;

        //    }
        //    set
        //    {
        //        try
        //        {
        //            _igniteClient = value;
        //        }
        //        catch (Exception ex)
        //        {
        //            throw ex;
        //        }
        //    }
        //}

        //public static ICache<TickKey, Tick> ICache
        //{
        //    get
        //    {
        //        return _icache;

        //    }
        //    set
        //    {
        //        try
        //        {
        //            _icache = value;
        //        }
        //        catch (Exception ex)
        //        {
        //            throw ex;
        //        }
        //    }
        //}

        //public static ICache<uint, Tick> IMarketDataCache
        //{
        //    get
        //    {
        //        return _imarketDataCache;

        //    }
        //    set
        //    {
        //        try
        //        {
        //            _imarketDataCache = value;
        //        }
        //        catch (Exception ex)
        //        {
        //            throw ex;
        //        }
        //    }
        //}

        //public static IContinuousQueryHandle IContinuousQuery
        //{
        //    get
        //    {
        //        return _iContinuousQuery;

        //    }
        //    set
        //    {
        //        try
        //        {
        //            _iContinuousQuery = value;
        //        }
        //        catch (Exception ex)
        //        {
        //            throw ex;
        //        }
        //    }
        //}
        //public static ICacheClient<TickKey, Tick> ICacheClient
        //{
        //    get
        //    {
        //        return _icacheClient;

        //    }
        //    set
        //    {
        //        try
        //        {
        //            _icacheClient = value;
        //        }
        //        catch (Exception ex)
        //        {
        //            throw ex;
        //        }
        //    }
        //}


        //public static IDataStreamer<TickKey, Tick> IDataStreamer
        //{
        //    get
        //    {
        //        return _idataStreamer;
        //    }
        //    set
        //    {
        //        try
        //        {
        //            _idataStreamer = value;
        //        }
        //        catch (Exception ex)
        //        {
        //            throw ex;
        //        }
        //    }
        //}

        //public static IDataStreamer<uint, Tick> IMarketDataStreamer
        //{
        //    get
        //    {
        //        return _imarketDataStreamer;
        //    }
        //    set
        //    {
        //        try
        //        {
        //            _imarketDataStreamer = value;
        //        }
        //        catch (Exception ex)
        //        {
        //            throw ex;
        //        }
        //    }
        //}

        //public static IMessaging IMessaging
        //{
        //    get
        //    {
        //        return _imessaging;
        //    }
        //    set
        //    {
        //        try
        //        {
        //            _imessaging = value;
        //        }
        //        catch (Exception ex)
        //        {
        //            throw ex;
        //        }
        //    }
        //}
    }
}
