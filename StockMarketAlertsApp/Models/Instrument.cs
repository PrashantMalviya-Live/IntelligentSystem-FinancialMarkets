namespace StockMarketAlertsApp.Models
{
    public class Instrument
    {
        public uint InstrumentToken { get; set; }
        public required string TradingSymbol { get; set; }
    }
}
