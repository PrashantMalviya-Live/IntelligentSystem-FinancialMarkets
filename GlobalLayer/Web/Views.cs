using System;
using System.Threading;
using System.Reflection;
using System.Collections.Generic;

namespace Global.Web
{
    public class ChartModel
    {
        public List<int> Data { get; set; }
        public string Label { get; set; }
        public ChartModel()
        {
            Data = new List<int>();
        }
    }
    public class BInstumentView
    {
        public UInt32 InstrumentToken { get; set; }
        public string TradingSymbol { get; set; }
    }
    public class OptionView
    {
        public UInt32 InstrumentToken { get; set; }
        public string TradingSymbol { get; set; }
        public string Type { get; set; }
        public decimal Strike { get; set; }
    }

    public class OrderView
    {
        public UInt32 InstrumentToken { get; set; }
        public string TradingSymbol { get; set; }
        public string TransactionType { get; set; }
        public decimal Price { get; set; }
        public decimal TriggerPrice { get; set; }
        public int Quantity { get; set; }
        public string Status { get; set; }
        public string StatusMessage { get; set; }
        public string Algorithm { get; set; }
        public int AlgoInstance { get; set; }
        public string OrderTime { get; set; }
        public string OrderType { get; set; }

    }
    public class LogDataView
    {
        public string TimeStamp { get; set; }
        public string Event { get; set; }
        public string Data { get; set; }
        
    }

    
}
