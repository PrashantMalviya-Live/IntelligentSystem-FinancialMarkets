using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Algos.Utilities.Views
{
    public class SARInputs
    {
        /// <summary>
        /// Base Instrument Token
        /// </summary>
        public uint BToken { get; set; }


        /// <summary>
        /// Initial quantity
        /// </summary>
        public int Qty { get; set; }

        /// <summary>
        /// User Id
        /// </summary>
        public string uid { get; set; }

        /// <summary>
        /// User Id
        /// </summary>
        public DateTime Expiry { get; set; }

        /// <summary>
        /// Futures data ??
        /// </summary>
        public bool FO { get; set; } = false;

        /// <summary>
        /// User Id
        /// </summary>
        public decimal tpr { get; set; } = 10;

        /// <summary>
        /// User Id
        /// </summary>
        public decimal sl { get; set; } = 20;

        /// <summary>
        /// threshold
        /// </summary>
        public decimal th { get; set; } = 30;

        /// <summary>
        /// Value Threshold
        /// </summary>
        public decimal vt { get; set; } = 300;
    }
}
