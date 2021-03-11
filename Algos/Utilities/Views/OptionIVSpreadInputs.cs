using System;
using System.Collections.Generic;
using System.Text;

namespace Algos.Utilities.Views
{
    public class OptionIVSpreadInput
    {
        /// <summary>
        /// Base Instrument Token
        /// </summary>
        public uint BToken { get; set; }

        /// <summary>
        /// Expiry
        /// </summary>
        public DateTime Expiry1 { get; set; }

        /// <summary>
        /// Expiry
        /// </summary>
        public DateTime Expiry2 { get; set; }

        /// <summary>
        /// Initial quantity
        /// </summary>
        public int Qty { get; set; }

        /// <summary>
        /// Target Profit
        /// </summary>
        public decimal TP { get; set; }

    }
}
