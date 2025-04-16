using System;
using System.Collections.Generic;
using System.Text;

namespace Algos.Utilities.Views
{
    public class OptionOptimizerInput
    {
        /// <summary>
        /// Base Instrument Token
        /// </summary>
        public uint BToken { get; set; }

        /// <summary>
        /// Expiry
        /// </summary>
        public DateTime E1 { get; set; }
        /// <summary>
        /// Expiry
        /// </summary>
        public DateTime E2 { get; set; }
        
        /// <summary>
        /// Expiry
        /// </summary>
        public DateTime E3 { get; set; }

        /// <summary>
        /// End Date Time
        /// </summary>
        public DateTime EDT { get; set; }

        /// <summary>
        /// Max Drawdown
        /// </summary>
        public decimal MDD { get; set; }



    }
}
