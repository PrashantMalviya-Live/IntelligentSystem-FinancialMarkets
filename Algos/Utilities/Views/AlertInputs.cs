using GlobalLayer;
using System;
using System.Collections.Generic;
using System.Text;

namespace Algos.Utilities.Views
{
    public class AlertInput
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
        /// Candle size in percent of base token value
        /// </summary>
        public decimal CSP { get; set; }
    }
}
