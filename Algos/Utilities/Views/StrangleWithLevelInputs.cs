using System;
using System.Collections.Generic;
using System.Text;
using GlobalLayer;
namespace Algos.Utilities.Views
{
    public class StrangleWithLevelInputs
    {
        /// <summary>
        /// Base Instrument Token
        /// </summary>
        public uint BToken { get; set; }

        /// <summary>
        /// Candle Time frame
        /// </summary>
        public int CTF { get; set; }

        /// <summary>
        /// Initial quantity
        /// </summary>
        public int IQty { get; set; }

        /// <summary>
        ///Step quantity
        /// </summary>
        public int SQty { get; set; }

        /// <summary>
        /// Max Quantity
        /// </summary>
        public int MQty { get; set; }

        /// <summary>
        /// No Trade Zone Lower Level 1
        /// </summary>
        public decimal L1 { get; set; }

        /// <summary>
        /// No Trade Zone Lower Level 1
        /// </summary>
        public decimal L2 { get; set; }

        /// <summary>
        /// No Trade Zone Lower Level 1
        /// </summary>
        public decimal U1 { get; set; }

        /// <summary>
        /// No Trade Zone Lower Level 1
        /// </summary>
        public decimal U2 { get; set; }

        /// <summary>
        /// Expiry
        /// </summary>
        public DateTime Expiry { get; set; }

        /// <summary>
        /// Target Profit for the trade
        /// </summary>
        public string uid { get; set; }

        /// <summary>
        /// Stop loss for the trade
        /// </summary>
        public decimal SL { get; set; }

        /// <summary>
        /// Target Profit for the trade
        /// </summary>
        public decimal TP { get; set; }

        /// <summary>
        /// Min Delta 
        /// </summary>
        public decimal MinDelta { get; set; }

        /// <summary>
        /// Max Delta
        /// </summary>
        public decimal MaxDelta { get; set; }

        /// <summary>
        /// Initial Delta
        /// </summary>
        public decimal IDelta { get; set; }

        public Order CallOrder { get; set; } = null;
        public Order PutOrder { get; set; } = null;
    }
}
