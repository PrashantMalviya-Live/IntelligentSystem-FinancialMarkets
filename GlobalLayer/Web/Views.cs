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
    public class ActiveAlgosView
    {
        //Algo ID
        public int aid { get; set; }
        //Algo Name
        public string an { get; set; }
        //Algo instance ID
        public int ains { get; set; }
        //expiry
        public string expiry { get; set; }
        //candle time frame (in mins)
        public int mins { get; set; }
        //base instrument
        public string binstrument { get; set; }
        //Lot size
        public int lotsize { get; set; }
        public decimal maxLossPerTrade { get; set; }
        //algo start date
        public string algodate { get; set; }
        //List of orders in the algo
        public List<OrderView> Orders { get; set; } = new List<OrderView>();
    }

    public class OrderView
    {
        public string orderid { get; set; }
        public UInt32 instrumenttoken { get; set; }
        public string tradingsymbol { get; set; }
        public string transactiontype { get; set; }
        public decimal price { get; set; }
        public decimal triggerprice { get; set; }
        public int quantity { get; set; }
        public string status { get; set; }
        public string statusmessage { get; set; }
        public string algorithm { get; set; }
        public int algoinstance { get; set; }
        public string ordertime { get; set; }
        public string ordertype { get; set; }

    }
    public class LogDataView
    {
        public string TimeStamp { get; set; }
        public string Event { get; set; }
        public string Data { get; set; }
        
    }

    
}
