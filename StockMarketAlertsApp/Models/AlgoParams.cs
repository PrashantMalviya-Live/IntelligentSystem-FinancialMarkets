namespace StockMarketAlertsApp.Models
{
    public class AlgoParams
    {

        public required int ID { get; set; }
        public required string Name { get; set; }
        public string InstrumentToken { get; set; }
        public string? TradingSymbol { get; set; }
        
        public string Quantity { get; set; }

        //in minutes
        public string CandleTimeFrame { get; set; }

        //user who has set this up
        public required string UserId { get; set; }

    }
}
