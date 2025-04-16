namespace StockMarketAlertsApp.Models
{
    public class PositionSummary
    {
        public string Product { get; set; }
        //public int OvernightQuantity { get; set; }
        public string Exchange { get; set; }
        public decimal SellValue { get; set; }
        //  public decimal BuyM2M { get; set; }
        public decimal LastPrice { get; set; } = 0;
        public string TradingSymbol { get; set; }
        //public decimal Realised { get; set; }
        public decimal PNL { get; set; } = 0;
        //public decimal Multiplier { get; set; }
        public int SellQuantity { get; set; }
        //public decimal SellM2M { get; set; }
        public decimal BuyValue { get; set; }
        public int BuyQuantity { get; set; }
        public decimal AveragePrice { get; set; }
        // public decimal Unrealised { get; set; }
        // public decimal Value { get; set; }
        //  public decimal BuyPrice { get; set; }
        // public decimal SellPrice { get; set; }
        //public decimal M2M { get; set; }
        public UInt32 InstrumentToken { get; set; }
        //public decimal ClosePrice { get; set; }
        //public int Quantity { get; set; }
        //public int DayBuyQuantity { get; set; }
        //public decimal DayBuyPrice { get; set; }
        //public decimal DayBuyValue { get; set; }
        //public int DaySellQuantity { get; set; }
        //public decimal DaySellPrice { get; set; }
        //public decimal DaySellValue { get; set; }

        public DateTime ExpiryDate { get; set; }


    }
}
