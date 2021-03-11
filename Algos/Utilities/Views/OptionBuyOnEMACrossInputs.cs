using System;
using System.Collections.Generic;
using System.Text;

namespace Algos.Utilities.Views
{
    public class OptionBuyOnEMACrossInputs
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
        /// Number of periods for longer EMA
        /// </summary>
        public int LEMA { get; set; }

        /// <summary>
        /// Number of periods for shorter EMA
        /// </summary>
        public int SEMA { get; set; }

        /// <summary>
        /// Initial quantity
        /// </summary>
        public int Qty { get; set; }

        /// <summary>
        /// Target Profit
        /// </summary>
        public decimal TP { get; set; }

        /// <summary>
        /// Stop Loss
        /// </summary>
        public decimal SL { get; set; }

    }
}
