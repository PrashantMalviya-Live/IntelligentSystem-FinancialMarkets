using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TradeEMACross
{
    public class OptionMomentumInput
    {
        /// <summary>
        /// Instrument Token
        /// </summary>
        public uint Token { get; set; }

        /// <summary>
        /// Number of Options to trade
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// Expiry
        /// </summary>
        public DateTime Expiry { get; set; }

        /// <summary>
        /// Candle Time Frame (in minutes)
        /// </summary>
        public int CTF { get; set; }
    }
}
