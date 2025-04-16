using System;
using QuestDB;
using GlobalLayer;
using QuestDB.Senders;
using System.Threading.Tasks;
using CsvHelper;
using Google.Protobuf.WellKnownTypes;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Drawing;
using InfluxDB.Client.Core;
using System.Runtime.InteropServices;

namespace DataAccess
{
    public class QuestDB
    {
        private static readonly ISender _sender = Sender.New("http::addr=localhost:9000;username=admin;password=quest;auto_flush_rows=100;auto_flush_interval=1000;");
        //public QuestDB() {

        //    _sender = Sender.New("http::addr=localhost:9000;username=admin;password=quest;auto_flush_rows=100;auto_flush_interval=1000;");
        //}
        public static async Task InsertObject(L1Orderbook data)
        {
            var now = DateTime.UtcNow;
            try
            {
                await _sender.Table("l1_orderbook")
                .Symbol("symbol", data.Symbol)
                .Column("ask_qty", data.AskQuantity)
                .Column("best_ask", data.BestAsk)
                .Column("best_bid", data.BestBid)
                .Column("bid_qty", data.BidQuantity)
                .Column("last_sequence_no", data.LastSequenceNo)
                .Column("last_updated_at", ConvertTime(data.LastUpdatedAt))
                .Column("product_id", data.ProductId)
                .Column("timestamp", ConvertTime(data.Timestamp))
                 .Column("type", data.Type)
                .AtAsync(now);
                await _sender.SendAsync();

            }
            catch (Exception ex)
            {

            }
        }
        public static async Task InsertObject(AllTradesResponse data)
        {
            var now = DateTime.UtcNow;
                await _sender.Table("all_trades")
                    .Symbol("symbol", data.Symbol)
                    .Column("price", data.Price)
                    .Column("size", data.Size)
                    .Column("type", data.Type)
                    .Column("buyer_role", data.BuyerRole)
                    .Column("seller_role", data.SellerRole)
                    .Column("timestamp", ConvertTime(data.Timestamp))
                   .AtAsync(now);
            await _sender.SendAsync();
        }

        public static async Task InsertObject(CandleSticksResponse data)
        {
            var now = DateTime.UtcNow;
            await _sender.Table("candlesticks")
                    .Symbol("symbol", data.Symbol)
                    .Column("candle_start_time", ConvertTime(data.CandleStartTime))
                    .Column("close", data.Close)
                    .Column("high", data.High)
                    .Column("low", data.Low)
                    .Column("open", data.Open)
                    .Column("resolution", data.Resolution)
                    .Column("timestamp", ConvertTime(data.Timestamp))
                    .Column("type", data.Type)
                    .Column("volume", data.Volume)
                    .AtAsync(now);

            await _sender.SendAsync();
        }

        public static async Task InsertObject(OrderUpdate data)
        {
            var now = DateTime.UtcNow;

            await _sender.Table("order_updates")
                .Symbol("symbol", data.Symbol)
                .Column("type", data.Type)
                .Column("action", data.Action)
                .Column("reason", data.Reason)
                .Column("product_id", data.ProductId)
                .Column("order_id", data.OrderId)
                .Column("client_order_id", data.ClientOrderId)
                .Column("size", data.Size)
                .Column("unfilled_size", data.UnfilledSize)
                .Column("average_fill_price", data.AverageFillPrice)
                .Column("limit_price", data.LimitPrice)
                .Column("side", data.Side)
                .Column("cancellation_reason", data.CancellationReason)
                .Column("stop_order_type", data.StopOrderType)
                .Column("bracket_order", data.BracketOrder)
                .Column("state", data.State)
                .Column("seq_no", data.SeqNo)
                .Column("timestamp", ConvertTime(data.Timestamp))
                .Column("stop_price", data.StopPrice)
                .Column("trigger_price_max_or_min", data.TriggerPriceMaxOrMin)
                .Column("bracket_stop_loss_price", data.BracketStopLossPrice)
                .Column("bracket_stop_loss_limit_price", data.BracketStopLossLimitPrice)
                .Column("bracket_take_profit_price", data.BracketTakeProfitPrice)
                .Column("bracket_take_profit_limit_price", data.BracketTakeProfitLimitPrice)
                .Column("bracket_trail_amount", data.BracketTrailAmount)
                .AtAsync(now);

            await _sender.SendAsync();
        }

        public static async Task InsertObject(CryptoOrder data)
        {
            var now = DateTime.UtcNow;

            await _sender.Table("CryptoOrder")
            .Symbol("side", data.Side)
            .Symbol("order_type", data.OrderType)
            .Symbol("stop_order_type", data.StopOrderType)
            .Symbol("state", data.State)
            .Symbol("product_symbol", data.ProductSymbol)
            .Column("id", data.Id)
            .Column("user_id", data.UserId)
            .Column("size", data.Size)
            .Column("unfilled_size", data.UnfilledSize)
            .Column("limit_price", data.LimitPrice)
            .Column("average_filled_price", data.AverageFilledPrice)
            .Column("stop_price", data.StopPrice)
            .Column("paid_commission", data.PaidCommission)
            .Column("commission", data.Commission)
            .Column("reduce_only", data.ReduceOnly)
            .Column("client_order_id", data.ClientOrderId)
            .Column("created_at", data.CreatedAt)
            .Column("product_id", data.ProductId)
            .Column("algo_instance_id", data.AlgoInstanceId)
            .Column("strategy_id", data.StrategyId)
                .AtAsync(now);

            await _sender.SendAsync();
        }

        public static async Task InsertObject(decimal algoInstance, decimal pnl, DateTime currentTime)
        {
            //var now = DateTime.UtcNow;

            await _sender.Table("AlgoPnl")
            .Symbol("algo_instance_id", algoInstance.ToString())
            .Symbol("pnl", pnl.ToString())
                .AtAsync(currentTime);

            await _sender.SendAsync();
        }

        private static DateTime ConvertTime(long microseconds)
        {
            // Convert microseconds to milliseconds
            long milliseconds = microseconds / 1000;

            // Convert to DateTime
            DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
            return dateTimeOffset.UtcDateTime; // Convert to UTC DateTime
        }
    }
}
