using GlobalLayer;
using System;
using System.Collections.Generic;
using System.Text;

namespace Algos.Utilities.Views
{
    public class OptionSellwithRSIInput
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
        /// Number of periods for EMA
        /// </summary>
        public int EMA { get; set; }

        /// <summary>
        /// Initial quantity
        /// </summary>
        public int Qty { get; set; }

        /// <summary>
        /// Min distance from BI
        /// </summary>
        public decimal MinDFBI { get; set; }

        /// <summary>
        /// Max distance from BI
        /// </summary>
        public decimal MaxDFBI { get; set; }

        ///// <summary>
        ///// Expiry
        ///// </summary>
        //public int AlgoInstance { get; set; }
        /// <summary>
        /// RSI Lower limit for entry
        /// </summary>
        public decimal RLLE { get; set; } = 65m;

        /// <summary>
        /// RSI Upper limit for entry
        /// </summary>
        public decimal RULE { get; set; } = 55m;

        ///// <summary>
        ///// Expiry
        ///// </summary>
        //public int AlgoInstance { get; set; }
        /// <summary>
        /// RSI band margin for exit
        /// </summary>
        public decimal RMX { get; set; } = 10;

        /// <summary>
        /// RSI limit for exit
        /// </summary>
        public decimal RLX { get; set; } = 60m;

        //public Order ActiveOrder { get; set; } = null;

        public Order CallOrder { get; set; } = null;
        public Order PutOrder { get; set; } = null;
    }
}
