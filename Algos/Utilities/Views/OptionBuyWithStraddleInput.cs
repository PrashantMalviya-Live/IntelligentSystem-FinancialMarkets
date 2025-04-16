using System;

namespace Algos.Utilities.Views
{
    public class OptionBuyWithStraddleInput
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

        /// <summary>
        /// Target Profit
        /// </summary>
        public decimal TR { get; set; } = 1.67m;

        /// <summary>
        /// Stop Loss
        /// </summary>
        public decimal SR { get; set; } = 1.3m;

        /// <summary>
        /// Straddle Shift
        /// </summary>
        public bool SS { get; set; }

        /// <summary>
        /// Straddle Shift
        /// </summary>
        public string UID { get; set; }

        /// <summary>
        /// Intraday
        /// </summary>
        public bool Intraday { get; set; } = true;


    }
}
