namespace StockMarketAlertsApp.Models
{
    public class SectorTrend : IComparable<SectorTrend>
    {
        public int Id { get; set; }
        public int SectorId { get; set; }

        public DateOnly TradeDate { get; set; }

        public decimal AveragePriceChange { get; set; }

        public int CompareTo(SectorTrend? st2)
        {
            ArgumentNullException.ThrowIfNull(st2);
            return this.TradeDate.CompareTo(st2.TradeDate);
        }
    }
}
