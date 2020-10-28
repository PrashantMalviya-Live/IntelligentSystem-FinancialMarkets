using System;
using System.Collections.Generic;
using System.Text;

namespace Algos.Utilities.Views
{
    public class OptionExpiryStrangleInput
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

        /// <summary>
        /// Expiry
        /// </summary>
        public DateTime Expiry { get; set; }
        /// <summary>
        /// Stop loss for the trade
        /// </summary>
        public int SL { get; set; }

        /// <summary>
        /// MinDistanceFromBInstrument
        /// </summary>
        public int MDFBI { get; set; }

        /// <summary>
        /// Minimum option premium to trade.Trading stops if premium goes below minimum option premium.
        /// </summary>
        public int MPTT { get; set; }
    }
}
