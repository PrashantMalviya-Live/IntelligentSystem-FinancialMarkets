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
        public int StepQty { get; set; }

        /// <summary>
        /// Initial quantity
        /// </summary>
        public int MaxQty { get; set; }


        /// <summary>
        /// Target Profit
        /// </summary>
        public decimal TP { get; set; }

        /// <summary>
        /// Stop Loss
        /// </summary>
        public decimal SL { get; set; }

        /// <summary>
        /// User Id
        /// </summary>
        public string UID { get; set; }

        /// <summary>
        /// Time out in minutes
        /// </summary>
        public double TO { get; set; }

        /// <summary>
        /// Target Profit
        /// </summary>
        public decimal OpenVar { get; set; }

        /// <summary>
        /// Target Profit
        /// </summary>
        public decimal CloseVar { get; set; }

        /// <summary>
        /// Target Profit
        /// </summary>
        public bool Straddle { get; set; } = false;

    }
}
