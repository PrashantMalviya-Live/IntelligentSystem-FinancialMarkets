using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Algos.Utilities.Views
{
    public class CryptoPriceActionInputs
    {
        /// <summary>
        /// Base Instrument Token
        /// </summary>
        public string BSymbol { get; set; }

        /// <summary>
        /// Initial quantity
        /// </summary>
        public int Qty { get; set; }

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

    }
}
