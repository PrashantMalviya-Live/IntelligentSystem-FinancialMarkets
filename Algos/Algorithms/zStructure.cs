using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Algos.TLogics
{
    /// <summary>
    /// Trade structure
    /// </summary>
    public struct BoxStatus
    {
        public BoxStatus(UInt32 instrumentToken, decimal avgPrice, int pendingqty):this()
        {
            //OrderId = data["order_id"];
            InstrumentToken = instrumentToken;
            AveragePrice = avgPrice;
            PendingQuantity = pendingqty;
        }

        //public string OrderId { get; set; }
        //public string ExchangeOrderId { get; set; }
        public UInt32 InstrumentToken { get; set; }
        public decimal AveragePrice { get; set; }
        public int PendingQuantity { get; set; }
    }
}
