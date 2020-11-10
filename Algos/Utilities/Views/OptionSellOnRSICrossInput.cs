using System;
using GlobalLayer;

namespace Algos.Utilities.Views
{
    public class OptionSellOnRSICrossInput
    {
        /// <summary>
        /// Base Instrument Token
        /// </summary>
        public uint BToken { get; set; }

        /// <summary>
        /// Expiry
        /// </summary>
        public DateTime Expiry { get; set; }

        /// <summary>
        /// Candle Time frame
        /// </summary>
        public int CTF { get; set; }

        /// <summary>
        /// Initial quantity
        /// </summary>
        public int Qty { get; set; }

        /// <summary>
        /// Min distance from BI
        /// </summary>
        public int MinDFBI { get; set; }

        /// <summary>
        /// Max distance from BI
        /// </summary>
        public int MaxDFBI { get; set; }

        /// <summary>
        /// RSI band margin for exit
        /// </summary>
        public decimal RMX { get; set; } = 10;
        public Order ActiveOrder { get; set; } = null;
    }
}
