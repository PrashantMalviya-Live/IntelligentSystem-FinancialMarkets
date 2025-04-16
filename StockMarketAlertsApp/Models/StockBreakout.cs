namespace StockMarketAlertsApp.Models
{
    public class StockBreakout
    {
        public int Id { get; set; }
        public uint InstrumentToken { get; set; }
        public required string TradingSymbol { get; set; }
        public required TimeSpan TimeFrame { get; set; }
        public decimal PercentageRetracement { get; set; }
        public bool Completed { get; set; }
        public bool Success { get; set; }
    }
    public class StockBreakoutView
    {
        public int Id { get; set; }
        public required uint InstrumentToken { get; set; }
        public required string TradingSymbol { get; set; }
        public required TimeSpan TimeFrame { get; set; }
        public decimal AveragePercentageRetracement { get; set; }
        public int TotalBreakouts { get; set; }
        public int CompletedCount { get; set; }
        public decimal SuccessCount { get; set; }
    }
}
