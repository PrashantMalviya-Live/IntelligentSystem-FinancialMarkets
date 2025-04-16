using GlobalLayer;
using System;
using System.Collections.Generic;
using System.Text;

namespace Algos.Utilities.Views
{
    public class PriceActionInput
    {
        /// <summary>
        /// Base Instrument Token
        /// </summary>
        public uint BToken { get; set; }

        /// <summary>
        /// Reference Instrument Token
        /// </summary>
        public uint? RToken { get; set; }

        /// <summary>
        /// Expiry
        /// </summary>
        public DateTime Expiry { get; set; }

        /// <summary>
        /// Expiry
        /// </summary>
        public DateTime? CurrentDate { get; set; }

        /// <summary>
        /// Candle Time frame
        /// </summary>
        public int CTF { get; set; }

        /// <summary>
        /// Initial quantity
        /// </summary>
        public int Qty { get; set; }

        /// <summary>
        /// Target Profit
        /// </summary>
        public decimal TP { get; set; } = 0;

        /// <summary>
        /// Stop Loss
        /// </summary>
        public decimal SL { get; set; } = 0;

        //public decimal PD_H { get; set; } = 0;

        /// <summary>
        /// Intraday flag
        /// </summary>
        public bool IntD { get; set; } = true;

        public decimal TP1 { get; set; } = 0;
        public decimal TP2 { get; set; } = 0;
        public decimal TP3 { get; set; } = 0;
        public decimal TP4 { get; set; } = 0;
        public decimal SL1 { get; set; } = 0;
        public decimal SL2 { get; set; } = 0;
        public decimal SL3 { get; set; } = 0;
        public decimal SL4 { get; set; } = 0;


        public string UID { get; set; }

        public int AID { get; set; } = 0;

        public decimal PnL { get; set; } = 0;

        public List<OrderTrio> ActiveOrderTrios { get; set; } = null;

        //Current breakout model
        public Breakout BM { get; set; } = Breakout.NONE;

        public PriceRange CPR { get; set; } = null;
        public PriceRange PPR { get; set; } = null;

    }
}
