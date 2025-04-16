using GlobalLayer;
using System;
using System.Collections.Generic;
using System.Text;

namespace Algos.Utilities.Views
{
    public class StraddleInput
    {
        /// <summary>
        /// Base Instrument Token
        /// </summary>
        public uint BToken { get; set; }


        /// <summary>
        /// Initial quantity
        /// </summary>
        public int Qty { get; set; }

        ///// <summary>
        ///// User Id
        ///// </summary>
        //public string uid { get; set; }

        /// <summary>
        /// User Id
        /// </summary>
        public DateTime Expiry { get; set; }

        /// <summary>
        /// Candle Time Span in minutes
        /// </summary>
        public int CTF { get; set; }

        /// <summary>
        /// Candle Time Span
        /// </summary>
        public bool IntraDay { get; set; } = true;

        /// <summary>
        /// Straddle Shift
        /// </summary>
        public bool SS { get; set; } = true;

        /// <summary>
        /// Threshold Ratio
        /// </summary>
        public decimal TR { get; set; }

        /// <summary>
        /// Target Profit
        /// </summary>
        public decimal TP { get; set; }

        /// <summary>
        /// User Id
        /// </summary>
        public string UID { get; set; }

        /// <summary>
        /// Strike Price Increment
        /// </summary>
        public int SPI { get; set; }

        /// <summary>
        /// Flag to be used anywhere
        /// </summary>
        public bool Flag { get; set; }

        /// <summary>
        /// Stop Loss
        /// </summary>
        public decimal SL { get; set; }

        public decimal PnL { get; set; }

        public List<OrderTrio> ActiveOrderTrios { get; set; } = null;
    }
}
