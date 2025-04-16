using System;
using System.Collections.Generic;
using System.Text;
using GlobalLayer;
namespace Algos.Utilities.Views
{
    public class OptionTradeInputs
    {
        /// <summary>
        /// Base Instrument Token
        /// </summary>
        public uint BToken { get; set; }

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
        /// Initial Delta
        /// </summary>
        public decimal IDelta { get; set; }
    }
}
