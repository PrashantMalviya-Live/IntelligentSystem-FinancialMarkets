using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AdvanceAlgos.Utilities;
using Confluent.Kafka;
using KafkaStreams;
using Global;
using KiteConnect;
using ZConnectWrapper;
//using Pub_Sub;
using MarketDataTest;
using System.Data;

namespace Algos.TLogics
{
    public class PivotCPRBasics : IObserver<Tick[]>
    {
        SortedDictionary<string, PivotInstrument> ActivePivotInstruments = new SortedDictionary<string, PivotInstrument>();

        public PivotCPRBasics()
        {
            LoadActiveData();
        }
        public virtual void OnNext(Tick[] ticks)
        {
            lock (ActivePivotInstruments)
            {
                for (int i = 0; i < ActivePivotInstruments.Count; i++)
                {
                    TradePivots(ActivePivotInstruments.ElementAt(i).Value, ticks);

                    //   foreach (KeyValuePair<int, StrangleNode> keyValuePair in ActiveStrangles)
                    // {
                    //  ReviewStrangle(keyValuePair.Value, ticks);
                    // }
                }
            }
        }

        private void TradePivots(PivotInstrument pivotInstrument, Tick[] ticks)
        {
            Instrument primaryInstrument = pivotInstrument.PrimaryInstrument;
            decimal lastBasePrice = primaryInstrument.LastPrice;

            decimal currentBasePrice = 0;
            
            Tick baseInstrumentTick = ticks.FirstOrDefault(x => x.InstrumentToken == primaryInstrument.InstrumentToken);
            if (baseInstrumentTick.LastPrice != 0)
            {
                primaryInstrument.LastPrice = currentBasePrice = baseInstrumentTick.LastPrice;
                pivotInstrument.PrimaryInstrument = primaryInstrument;
            }
            else { return; }
            foreach (KeyValuePair<string, PivotInstrument> subPivotInstrument in pivotInstrument.SubInstruments)
            {
                PivotInstrument subPI = subPivotInstrument.Value;
                Instrument subInstrument = subPI.PrimaryInstrument;
                Tick tick = ticks.FirstOrDefault(x => x.InstrumentToken == subInstrument.InstrumentToken);
                if (tick.LastPrice != 0)
                {
                    subInstrument.LastPrice = currentBasePrice = tick.LastPrice;
                    subPI.PrimaryInstrument = subInstrument;
                    pivotInstrument.SubInstruments[subPivotInstrument.Key] = subPI;
                }
            }
            
            if (lastBasePrice == 0)
            { return; }

            CentralPivotRange cpr = pivotInstrument.CPR;

            ///Logic: If BPR or UPR is crossed buy/sell with SL
            ///If R1, S1 is crossed , increase lots with SL

            ///TODO:Below needs to come from database and initial inputs
            //pivotInstrument.TradedLot = 0;
            //pivotInstrument.MaximumTradeLot = 1;
            //pivotInstrument.TradingUnitInLots = 1;

            decimal nearestStrikePrice = 0, triggerPrice = 0;
            Pivot pivot = null ;

            //Market is up. As long as it is above pivot
            if(lastBasePrice >= cpr.CentralPivot.Price[Constants.HIGH])
            {

            }







            if (lastBasePrice <= cpr.CentralPivot.Price[Constants.HIGH] && currentPrice > cpr.CentralPivot.Price[Constants.HIGH])
            {
                triggerPrice = cpr.CentralPivot.Price[Constants.HIGH];
                pivot = cpr.CentralPivot;
            }
            else if (lastPrice <= cpr.R[0].Price[Constants.HIGH] && currentPrice > cpr.R[0].Price[Constants.HIGH])
            {
                triggerPrice = cpr.R[0].Price[Constants.HIGH];
                pivot = cpr.R[0];
            }
            else if (lastPrice <= cpr.R[1].Price[Constants.HIGH] && currentPrice > cpr.R[1].Price[Constants.HIGH])
            {
                triggerPrice = cpr.R[1].Price[Constants.HIGH];
                pivot = cpr.R[1];
            }
            else if (lastPrice <= cpr.R[2].Price[Constants.HIGH] && currentPrice > cpr.R[2].Price[Constants.HIGH])
            {
                triggerPrice = cpr.R[2].Price[Constants.HIGH];
                pivot = cpr.R[2];
            }
            else if (lastPrice >= cpr.S[0].Price[Constants.LOW] && currentPrice < cpr.S[0].Price[Constants.LOW])
            {
                triggerPrice = cpr.S[0].Price[Constants.LOW];
                pivot = cpr.S[0];
            }
            else if (lastPrice >= cpr.S[1].Price[Constants.LOW] && currentPrice < cpr.S[1].Price[Constants.LOW])
            {
                triggerPrice = cpr.S[1].Price[Constants.LOW];
                pivot = cpr.S[1];
            }
            else if (lastPrice >= cpr.S[2].Price[Constants.LOW] && currentPrice < cpr.S[2].Price[Constants.LOW])
            {
                triggerPrice = cpr.S[2].Price[Constants.LOW];
                pivot = cpr.S[2];
            }

            if (triggerPrice != 0 && Math.Abs(pivotInstrument.TradedLot) < pivotInstrument.MaximumTradeLot)
            {
                triggerPrice = 0;
                nearestStrikePrice = Math.Round(triggerPrice / 100, 0, MidpointRounding.AwayFromZero) * 100;

                //Buy instrument
                ShortTrade trade = PlaceOrder(pivot.InstrumentToTrade.TradingSymbol, true, currentPrice,
                    Convert.ToInt32(primaryInstrument.LotSize) * pivotInstrument.TradingUnitInLots,
                    baseInstrumentTick.Timestamp, pivot.InstrumentToTrade.InstrumentToken);
                pivotInstrument.TradedLot += Convert.ToInt32(trade.Quantity / primaryInstrument.LotSize);
                //pivotInstrument.LastTradedPrice = trade.AveragePrice;

                //pivot trade instrument for which this traded instrument is primary should be sent
                UpdateTradeDetails(pivotInstrument, trade);
            }

            else if (lastPrice >= cpr.CentralPivot.Price[Constants.LOW] && currentPrice < cpr.CentralPivot.Price[Constants.LOW])
            {
                triggerPrice = cpr.CentralPivot.Price[Constants.LOW];
                pivot = cpr.CentralPivot;
            }
            else if (lastPrice >= cpr.R[0].Price[Constants.LOW] && currentPrice < cpr.R[0].Price[Constants.LOW])
            {
                triggerPrice = cpr.R[0].Price[Constants.LOW];
                pivot = cpr.R[0];
            }
            else if (lastPrice >= cpr.R[1].Price[Constants.LOW] && currentPrice < cpr.R[1].Price[Constants.LOW])
            {
                triggerPrice = cpr.R[1].Price[Constants.LOW];
                pivot = cpr.R[1];
            }
            else if (lastPrice >= cpr.R[2].Price[Constants.LOW] && currentPrice < cpr.R[2].Price[Constants.LOW])
            {
                triggerPrice = cpr.R[2].Price[Constants.LOW];
                pivot = cpr.R[2];
            }
            else if (lastPrice <= cpr.S[0].Price[Constants.HIGH] && currentPrice > cpr.S[0].Price[Constants.HIGH])
            {
                triggerPrice = cpr.S[0].Price[Constants.HIGH];
                pivot = cpr.S[0];
            }
            else if (lastPrice <= cpr.S[1].Price[Constants.HIGH] && currentPrice > cpr.S[1].Price[Constants.HIGH])
            {
                triggerPrice = cpr.S[1].Price[Constants.HIGH];
                pivot = cpr.S[1];
            }
            else if (lastPrice <= cpr.S[2].Price[Constants.HIGH] && currentPrice > cpr.S[2].Price[Constants.HIGH])
            {
                triggerPrice = cpr.S[2].Price[Constants.HIGH];
                pivot = cpr.S[2];
            }

            if (triggerPrice != 0 && pivotInstrument.TradedLot >= pivotInstrument.TradingUnitInLots)
            {
                triggerPrice = 0;
                nearestStrikePrice = Math.Round(triggerPrice / 100, 0, MidpointRounding.AwayFromZero) * 100;

                //Close instrument
                ShortTrade trade = PlaceOrder(pivot.InstrumentToTrade.TradingSymbol, false, currentPrice,
                    Convert.ToInt32(primaryInstrument.LotSize) * pivotInstrument.TradingUnitInLots,
                    baseInstrumentTick.Timestamp, pivot.InstrumentToTrade.InstrumentToken);
                pivotInstrument.TradedLot -= Convert.ToInt32(trade.Quantity / primaryInstrument.LotSize);
                //pivotInstrument.LastTradedPrice = trade.AveragePrice;

                //pivot trade instrument for which this traded instrument is primary should be sent
                UpdateTradeDetails(pivotInstrument, trade);
            }

            //// Initial Trade & Stop Loss fir Pivot
            //if (lastPrice != 0 && (((lastPrice <= cpr.CentralPivot.Price[Constants.HIGH] && currentPrice > cpr.CentralPivot.Price[Constants.HIGH])
            //    || (lastPrice <= cpr.R[0].Price[Constants.HIGH] && currentPrice > cpr.R[0].Price[Constants.HIGH])
            //    || (lastPrice <= cpr.R[1].Price[Constants.HIGH] && currentPrice > cpr.R[1].Price[Constants.HIGH])
            //    || (lastPrice <= cpr.R[2].Price[Constants.HIGH] && currentPrice > cpr.R[2].Price[Constants.HIGH]))
            //    ||
            //    (((lastPrice <= cpr.S[0].Price[Constants.HIGH] && currentPrice > cpr.S[0].Price[Constants.HIGH])
            //    || (lastPrice <= cpr.S[1].Price[Constants.HIGH] && currentPrice > cpr.S[1].Price[Constants.HIGH])
            //    || (lastPrice <= cpr.S[2].Price[Constants.HIGH] && currentPrice > cpr.S[2].Price[Constants.HIGH])))

            //    ))
            //{
            //    if (Math.Abs(pivotInstrument.TradedLot) < pivotInstrument.MaximumTradeLot)
            //    {
            //        //Buy instrument
            //        ShortTrade trade = PlaceOrder(primaryInstrument.TradingSymbol, true, currentPrice,
            //            Convert.ToInt32(primaryInstrument.LotSize) * pivotInstrument.TradingUnitInLots,
            //            baseInstrumentTick.Timestamp, primaryInstrument.InstrumentToken);
            //        pivotInstrument.TradedLot += Convert.ToInt32(trade.Quantity / primaryInstrument.LotSize);
            //        pivotInstrument.LastTradedPrice = trade.AveragePrice;

            //        //pivot trade instrument for which this traded instrument is primary should be sent
            //        UpdateTradeDetails(pivotInstrument, trade);
            //    }
            //}
            //else if (lastPrice != 0 && (((lastPrice >= cpr.CentralPivot.Price[Constants.LOW] && currentPrice < cpr.CentralPivot.Price[Constants.LOW])
            //    || (lastPrice >= cpr.S[0].Price[Constants.LOW] && currentPrice < cpr.S[0].Price[Constants.LOW])
            //    || (lastPrice >= cpr.S[1].Price[Constants.LOW] && currentPrice < cpr.S[1].Price[Constants.LOW])
            //    || (lastPrice >= cpr.S[2].Price[Constants.LOW] && currentPrice < cpr.S[2].Price[Constants.LOW]))
                
            //    ||
                
            //    (((lastPrice >= cpr.R[0].Price[Constants.LOW] && currentPrice < cpr.R[0].Price[Constants.LOW])
            //    || (lastPrice >= cpr.R[1].Price[Constants.LOW] && currentPrice < cpr.R[1].Price[Constants.LOW])
            //    || (lastPrice >= cpr.R[2].Price[Constants.LOW] && currentPrice < cpr.R[2].Price[Constants.LOW])))
            //    ))
            //{
            //    if (pivotInstrument.TradedLot >= pivotInstrument.MaximumTradeLot)
            //    {
            //        //Buy instrument
            //        ShortTrade trade = PlaceOrder(primaryInstrument.TradingSymbol, false, currentPrice,
            //            Convert.ToInt32(primaryInstrument.LotSize) * pivotInstrument.TradingUnitInLots,
            //            baseInstrumentTick.Timestamp, primaryInstrument.InstrumentToken);
            //        pivotInstrument.TradedLot += Convert.ToInt32(trade.Quantity / primaryInstrument.LotSize);
            //        pivotInstrument.LastTradedPrice = trade.AveragePrice;

            //        //pivot trade instrument for which this traded instrument is primary should be sent
            //        UpdateTradeDetails(pivotInstrument, trade);
            //    }
            //}
        }
        private string GenerateTradingSymbol(string baseInstrumentSymbol, decimal strikePrice, DateTime? expiry, string instrumentType)
        {
            return string.Format("{0}{1}{2}{3}", baseInstrumentSymbol.Substring(0, baseInstrumentSymbol.LastIndexOf("FUT")), expiry, strikePrice, instrumentType);
        }
        private ShortTrade PlaceOrder(string tradingSymbol, bool buyOrder, decimal currentPrice, int quantity, DateTime? tickTime = null, uint token = 0)
        {
            //Dictionary<string, dynamic> orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, tradingSymbol.TrimEnd(),
            //                          buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL, quantity, Product: Constants.PRODUCT_MIS,
            //                          OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);

            ///TEMP, REMOVE Later
            if (currentPrice == 0)
            {
                DataLogic dl = new DataLogic();
                currentPrice = dl.RetrieveLastPrice(token, tickTime, buyOrder);
            }

            string orderId = "0";
            decimal averagePrice = 0;
            ////if (orderStatus["data"]["order_id"] != null)
            ////{
            ////    orderId = orderStatus["data"]["order_id"];
            ////}
            //if (orderId != "0")
            //{
            //    System.Threading.Thread.Sleep(200);
            //    List<Order> orderInfo = ZObjects.kite.GetOrderHistory(orderId);
            //    averagePrice = orderInfo[orderInfo.Count - 1].AveragePrice;
            //}
            if (averagePrice == 0)
                averagePrice = buyOrder ? currentPrice + 1 : currentPrice - 1;
            averagePrice = buyOrder ? averagePrice * -1 : averagePrice;

            ShortTrade trade = new ShortTrade();
            trade.AveragePrice = averagePrice;
            trade.ExchangeTimestamp = DateTime.Now;
            trade.Quantity = buyOrder ? quantity : quantity * -1;
            trade.OrderId = orderId;
            trade.TransactionType = buyOrder ? "Buy" : "Sell";

            return trade;
        }

        private void UpdateTradeDetails(PivotInstrument pivotInstrument, ShortTrade trade)
        {
            DataLogic dl = new DataLogic();
            dl.UpdateTrade(pivotInstrument.StrategyID, pivotInstrument.PrimaryInstrument.InstrumentToken, trade, AlgoIndex.PivotedBasedTrade, pivotInstrument.TradedLot);
        }
        private void LoadActiveData()
        {
            AlgoIndex algoIndex = AlgoIndex.PivotedBasedTrade;
            DataLogic dl = new DataLogic();
            DataSet activePivotInstruments = dl.RetrieveActivePivotInstruments(algoIndex);

            DataRelation strategy_Token_Relation = activePivotInstruments.Relations.Add("Strategy_Instrument", new DataColumn[] { activePivotInstruments.Tables[0].Columns["StrategyID"] },
                new DataColumn[] { activePivotInstruments.Tables[1].Columns["StrategyId"] });

            DataRelation strategy_Trades_Relation = activePivotInstruments.Relations.Add("Instrument_Prices", new DataColumn[] { activePivotInstruments.Tables[1].Columns["InstrumentToken"] },
                new DataColumn[] { activePivotInstruments.Tables[2].Columns["InstrumentToken"] });

            PivotInstrument pivotInstrument = new PivotInstrument();
            PivotInstrument subPivotInstruments = null;

            foreach (DataRow piRow in activePivotInstruments.Tables[0].Rows)
            {
                foreach (DataRow strangleTokenRow in piRow.GetChildRows(strategy_Token_Relation))
                {

                    Instrument instrument = new Instrument()
                    {
                        //BaseInstrumentToken = Convert.ToUInt32(strangleTokenRow["BInstrumentToken"]),
                        InstrumentToken = Convert.ToUInt32(strangleTokenRow["InstrumentToken"]),
                        InstrumentType = (string)strangleTokenRow["Instrument_Type"],
                        Strike = (Decimal)strangleTokenRow["Strike"],
                        TradingSymbol = (string)strangleTokenRow["TradingSymbol"],
                        LotSize = Convert.ToUInt32(strangleTokenRow["Lot_size"])
                    };

                    if (strangleTokenRow["Expiry"] != DBNull.Value)
                    {
                        instrument.Expiry = Convert.ToDateTime(strangleTokenRow["Expiry"]);
                    }

                    DataRow OHLC = strangleTokenRow.GetChildRows(strategy_Trades_Relation)[0];

                    CentralPivotRange cpr = new CentralPivotRange(GetOHLC(OHLC))
                    {
                        PivotFrequency = (PivotFrequency)piRow["PivotFrequency"],
                        PivotFrequencyWindow = (int?)piRow["PivotWindow"]
                    };

                    //if (strangleTokenRow["PivotFrequencyWindow"] != DBNull.Value)
                    //{
                    //    cpr.PivotFrequencyWindow = Convert.ToInt32(strangleTokenRow["PivotFrequencyWindow"]);
                    //}

                    if ((bool)strangleTokenRow["IsPrimary"] == true)
                    {
                        pivotInstrument.CPR = cpr;
                        pivotInstrument.TradedLot = Convert.ToInt32(piRow["TradedLot"]);
                        pivotInstrument.MaximumTradeLot = Convert.ToInt32(piRow["MaximumTradeLot"]);
                        pivotInstrument.TradingUnitInLots = Convert.ToInt32(piRow["TradingUnitInLots"]);
                        pivotInstrument.PrimaryInstrument = instrument;
                    }
                    else
                    {
                        subPivotInstruments = new PivotInstrument();
                        subPivotInstruments.CPR = cpr;
                        subPivotInstruments.PrimaryInstrument = instrument;

                        if (pivotInstrument.SubInstruments == null)
                        {
                            pivotInstrument.SubInstruments = new SortedDictionary<string, PivotInstrument>();
                        }
                        pivotInstrument.SubInstruments.Add(instrument.TradingSymbol, subPivotInstruments);
                    }
                }
            }

            if (pivotInstrument != null && pivotInstrument.PrimaryInstrument.TradingSymbol != null)
            {
                ActivePivotInstruments.Add(pivotInstrument.PrimaryInstrument.TradingSymbol, pivotInstrument);
            }
        }

        public void StorePivotInstruments(uint primaryInst, DateTime expiry, int initialQty, int maxQty, int stepQty,
            int pivotFrequency, int pivotWindow, DateTime timeOfOrder = default(DateTime))
        {
            //Update Database
            DataLogic dl = new DataLogic();
            dl.StorePivotInstruments(primaryInst, expiry, AlgoIndex.PivotedBasedTrade, maxQty, stepQty, (PivotFrequency) pivotFrequency, pivotWindow);
        }

        private OHLC GetOHLC(DataRow ohlcRow)
        {
            OHLC ohlc = new OHLC();
            ohlc.Close = Convert.ToDecimal(ohlcRow["Close"]);
            ohlc.Open = Convert.ToDecimal(ohlcRow["Open"]);
            ohlc.High = Convert.ToDecimal(ohlcRow["High"]);
            ohlc.Low = Convert.ToDecimal(ohlcRow["Low"]);
            return ohlc;
        }

        #region Pub Sub Methods
        public IDisposable UnsubscriptionToken;
        public virtual void OnError(Exception ex)
        {
            ///TODO: Log the error. Also handle the error.
        }

        public virtual void Subscribe(Publisher publisher)
        {
            UnsubscriptionToken = publisher.Subscribe(this);
        }
        public virtual void Subscribe(Ticker publisher)
        {
            UnsubscriptionToken = publisher.Subscribe(this);
        }

        public virtual void Subscribe(TickDataStreamer publisher)
        {
            UnsubscriptionToken = publisher.Subscribe(this);
        }


        public virtual void Unsubscribe()
        {
            UnsubscriptionToken.Dispose();
        }

        public virtual void OnCompleted()
        {
        }
        #endregion
    }
}
