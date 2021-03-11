using System;
using System.Collections.Generic;
using System.Text;

namespace Algos.Utilities.Views
{
    public class PremiumCrossInput
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
        /// Target Profit
        /// </summary>
        public decimal TP { get; set; } = 0;

        /// <summary>
        /// Stop Loss
        /// </summary>
        public decimal SL { get; set; } = 0;

    }
}
