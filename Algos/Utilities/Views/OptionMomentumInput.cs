using System;
using GlobalLayer;

namespace Algorithms.Utilities
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

        /// <summary>
        /// PositionSizing
        /// </summary>
        public bool PS { get; set; }

        /// <summary>
        /// MaxLossPerTrade
        /// </summary>
        public decimal MLPT { get; set; }

        public Order ActiveOrder { get; set; } = null;
    }
}
