namespace StockMarketAlertsApp.Models
{
    public class AlertMessage
    {
        public string ID { get; set; }
        public int AlertTriggerID { get; set; }
        public required uint InstrumentToken { get; set; }
        public required string TradingSymbol { get; set; }

        public required string UserId { get; set; }

        public required decimal LastPrice { get; set; }

        public required string Message { get; set; }

        public required string Criteria { get; set; }
        public required int CandleTimeSpan { get; set; }

        public DateTime TriggeredDateTime { get; set; }

        //collection of alert mode ids
        public string AlertModes { get; set; }

    }
}
