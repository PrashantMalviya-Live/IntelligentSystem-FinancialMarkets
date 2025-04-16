using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KiteConnect;
using KotakConnect;
using FyersConnect;
using DeltaExchangeConnect;
//using KiteConnectTicker;

namespace BrokerConnectWrapper
{
    public static class ZObjects
    {
        public static Ticker ticker;
        public static Kite kite;
        public static Kotak kotak;
        public static Fy fy;
        public static DeltaExchange deltaExchange;
        public static DeltaExchangeTicker deltaExchangeTicker;
    }
}
