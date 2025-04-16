using System;
using System.Collections.Generic;
using System.Text;

namespace Algos.Utilities.Views
{
    public class ChartDataInputs
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
        /// Expiry
        /// </summary>
        public string IT1 { get; set; }

        /// <summary>
        /// Expiry
        /// </summary>
        public string IT2 { get; set; }

        public bool CMB { get; set; }

        public bool IVC { get; set; }

        public bool OIC { get; set; }

        public decimal S1 { get; set; }

        public decimal S2 { get; set; }


        ///// <summary>
        ///// Initial quantity
        ///// </summary>
        //public int Qty { get; set; }

        ///// <summary>
        ///// Target Profit
        ///// </summary>
        //public decimal TP { get; set; }

    }
}
