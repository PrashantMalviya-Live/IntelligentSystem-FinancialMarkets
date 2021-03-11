using GlobalLayer;
using System;
using System.Collections.Generic;
using System.Text;

namespace Algos.Utilities.Views
{
    public class OptionBuywithRSIInput
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

        /// <summary>
        /// Target Profit
        /// </summary>
        public decimal TP { get; set; }

        /// <summary>
        /// Target Profit
        /// </summary>
        public decimal SL { get; set; }

        /// <summary>
        /// Lower level for CE buy
        /// </summary>
        public decimal CELL { get; set; }

        /// <summary>
        /// Upper level for PE buy
        /// </summary>
        public decimal PEUL { get; set; }


        /// <summary>
        /// RSI Upper Limit for Exit
        /// </summary>
        public decimal RULX { get; set; }

        /// <summary>
        /// RSI Lower Limit for entry
        /// </summary>
        public decimal RLLE { get; set; }

        /// <summary>
        /// Option Buy at Cross
        /// </summary>
        public bool EAC { get; set; }

        /// <summary>
        /// Entry Decisive Candle High Low
        /// </summary>
        public decimal eDCHL { get; set; }

        /// <summary>
        /// Exit Decisive Candle High Low
        /// </summary>
        public decimal xDCHL { get; set; }

        /// <summary>
        /// Future contract
        /// </summary>
        public bool Fut { get; set; }

        public Order CallOrder { get; set; } = null;
        public Order PutOrder { get; set; } = null;

        public Order Order { get; set; } = null;
    }
}
