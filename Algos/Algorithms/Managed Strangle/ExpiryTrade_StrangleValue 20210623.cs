using Algorithms.Candles;
using Algorithms.Indicators;
using Algorithms.Utilities;
using Algorithms.Utils;
using GlobalCore;
using GlobalLayer;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZConnectWrapper;
using ZMQFacade;
namespace Algos.TLogics
{

    /// <summary>
    /// Stratety based on total strangle value.
    /// Increase lots on strangle till, 1 side becomes half 
    /// Start with 5 lots (5x40) and then book 2 lots when strangle looses 20% value, and 3 lots when strangle looses 50% value and then move 1 point near on both side or form appropriate strangle
    /// If strangle value increases by 20%, add 2 lots; another 2 lots on the side when 1 side becomes 50% of another side
    /// keep a check on next strangle price and close the profit side and make another strangle with high size. This way strangle will get near the stock price
    /// Try delta neutral
    /// Do this till next strike price cross one of strangle strike
    /// </summary>
    public class ExpiryTrade : IZMQ //ITibcoListener , IMessageListener<Tick> , ICacheEntryEventListener<TickKey, Tick>,ITibcoListener//, IKafkaConsumer//, IObserver<Tick[]>
    {
        public IDisposable UnsubscriptionToken;
        Dictionary<int, StrangleDataStructure> ActiveStrangles;
        private int _algoInstance;

        private const int INSTRUMENT_TOKEN = 0;
        private const int INITIAL_TRADED_PRICE = 1;
        private const int CURRENT_PRICE = 2;
        private const int QUANTITY = 3;
        private const int TRADE_ID = 4;
        private const int TRADING_STATUS = 5;
        private const int POSITION_PnL = 6;
        private const int STRIKE = 7;
        private const int PRICEDELTA = 8;
        private bool ContinueTrade = false;
        private bool InitialOI = false;
        private const int CE = 0;
        private const int PE = 1;
        private const AlgoIndex algoIndex = AlgoIndex.ExpiryTrade;

        Dictionary<uint, ExponentialMovingAverage> lTokenEMA;
        Dictionary<uint, RelativeStrengthIndex> tokenRSI;
        List<uint> _EMALoaded;
        List<uint> _SQLLoading;
        Dictionary<uint, bool> _firstCandleOpenPriceNeeded;
        CandleManger candleManger;
        Dictionary<uint, List<Candle>> TimeCandles;

        public List<uint> SubscriptionTokens;
        private System.Timers.Timer _healthCheckTimer;
        private int _healthCounter = 0;

        private System.Timers.Timer _loggerTimer;

        private readonly uint _bInstrumentToken;
        private readonly DateTime _expiry;
        private readonly int _initialQty;
        private readonly int _stepQty;
        private readonly int _maxQty;
        private readonly decimal _stopLoss;
        private readonly decimal _targetProfit;
        private readonly int _minDistanceFromBInstrument;
        private readonly int _initialDistanceFromBInstrument;
        private readonly decimal _minPremiumToTrade;
        private bool _stopTrade = false;


        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(ExpiryTrade source);
        [field: NonSerialized]
        public event OnOptionUniverseChangeHandler OnOptionUniverseChange;

        [field: NonSerialized]
        public delegate void OnTradeEntryHandler(Order st);
        [field: NonSerialized]
        public event OnTradeEntryHandler OnTradeEntry;

        [field: NonSerialized]
        public delegate void OnTradeExitHandler(Order st);
        [field: NonSerialized]
        public event OnTradeExitHandler OnTradeExit;

        public ExpiryTrade(uint bInstrumentToken, DateTime expiry, int initialQty, int stepQty, 
            int maxQty, decimal stopLoss, int minDistanceFromBInstrument, decimal minPremiumToTrade, int initialDistanceFromBInstrument, decimal targetProfit)
        {
            ActiveStrangles = new Dictionary<int, StrangleDataStructure>();
            _bInstrumentToken = bInstrumentToken;
            _expiry = expiry;
            _initialQty = initialQty;
            _stepQty = stepQty;
            _maxQty = maxQty;
            _stopLoss = stopLoss;
            _targetProfit = targetProfit;
            _minDistanceFromBInstrument = minDistanceFromBInstrument;
            _initialDistanceFromBInstrument = initialDistanceFromBInstrument;
            _minPremiumToTrade = minPremiumToTrade;

            _algoInstance = Utility.GenerateAlgoInstance(algoIndex, _bInstrumentToken, DateTime.Now, _expiry, initialQtyInLotsSize: _initialQty,
               maxQtyInLotSize: _maxQty, stepQtyInLotSize: _stepQty, stopLossPoints: (float) _stopLoss, upperLimit: _minDistanceFromBInstrument,
               lowerLimit: _minPremiumToTrade, Arg2: targetProfit, Arg1: initialDistanceFromBInstrument);


            LoadActiveData(_algoInstance, _bInstrumentToken, _expiry, _maxQty, _initialQty, _stepQty, 100, _minDistanceFromBInstrument, _minPremiumToTrade);
            
            //Load Data from database
            //LoadActiveData();
            
            SubscriptionTokens = new List<uint>();
            SubscriptionTokens.AddRange(ActiveStrangles.Values.Select(x => x.BaseInstrumentToken));

            //_algoInstance = StrangleNode.ID; Utility.GenerateAlgoInstance(algoIndex, DateTime.Now);

            ZConnect.Login();

            //health check after 1 mins
            _healthCheckTimer = new System.Timers.Timer(interval: 1 * 60 * 1000);
            _healthCheckTimer.Elapsed += _healthCheckTimer_Elapsed;
            _healthCheckTimer.Start();

            //Screen Update every 5 min
            _loggerTimer = new System.Timers.Timer(interval: 5 * 60 * 1000);
            _loggerTimer.Elapsed += __loggerTimer_Elapsed;
            _loggerTimer.Start();

            //RSI and EMA
            //lTokenEMA = new Dictionary<uint, ExponentialMovingAverage>();
            //tokenRSI = new Dictionary<uint, RelativeStrengthIndex>();
            //_EMALoaded = new List<uint>();
            //_SQLLoading = new List<uint>();
            //_firstCandleOpenPriceNeeded = new Dictionary<uint, bool>();
            //CandleSeries candleSeries = new CandleSeries();
            //candleManger = new CandleManger(TimeCandles, CandleType.Time);
            //candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;

        }

        //private void CandleManger_TimeCandleFinished(object sender, Candle e)
        //{
        //    throw new NotImplementedException();
        //}

        private void ManageStrangle(StrangleDataStructure strangleNode, Tick tick)
        {
            try
            {
                if (!GetBaseInstrumentPrice(tick, strangleNode))
                {
                    return;
                }
                if(!PopulateReferenceStrangleData(strangleNode, tick))
                {
                    return;
                }

                ///Commented Max pain for now. This will be used lated to predict the direction after more research.
                //decimal maxPainStrike = UpdateMaxPainStrike(strangleNode, tick);
                //if (maxPainStrike == 0)
                //{
                //    return;
                //}

                DateTime timeOfOrder = tick.LastTradeTime.HasValue ? tick.LastTradeTime.Value : tick.Timestamp.Value;
                SortedList<decimal, Instrument> callUniverse = strangleNode.CallUniverse;
                SortedList<decimal, Instrument> putUniverse = strangleNode.PutUniverse;

                Decimal[][,] optionMatix = strangleNode.OptionMatrix;

                //Each trade should have a record and pnl, and then this trade should be closed first when delta to be neutralized.

                if (optionMatix[0] == null)
                {
                    //int tradeCounter = 0;
                    //Take Initial Position
                    decimal[][] optionTrade = InitialTrade(strangleNode, tick);
                    for (int i = 0; i < optionTrade.GetLength(0); i++)
                    {
                        optionMatix[i] = new decimal[1, 10];
                        optionMatix[i][0, INSTRUMENT_TOKEN] = optionTrade[i][INSTRUMENT_TOKEN]; //Instrument Token
                        optionMatix[i][0, INITIAL_TRADED_PRICE] = optionTrade[i][INITIAL_TRADED_PRICE]; //TradedPrice
                        optionMatix[i][0, CURRENT_PRICE] = optionTrade[i][CURRENT_PRICE]; //CurrentPrice
                        optionMatix[i][0, QUANTITY] = optionTrade[i][QUANTITY]; //Quantity
                        optionMatix[i][0, TRADE_ID] = optionTrade[i][TRADE_ID]; //TradeID
                        optionMatix[i][0, TRADING_STATUS] = optionTrade[i][TRADING_STATUS]; // Trading Status: Open
                        optionMatix[i][0, POSITION_PnL] = optionTrade[i][POSITION_PnL];// PnL of trade as price updates
                        optionMatix[i][0, STRIKE] = optionTrade[i][STRIKE];// Strike Price of Instrument

                        //string tradingSymbol = i == PE ? strangleNode.PutUniverse[optionTrade[i][STRIKE]].TradingSymbol : strangleNode.CallUniverse[optionTrade[i][STRIKE]].TradingSymbol;


                        //callMatix[tradeCounter, PRICEDELTA] = callTrade[1];// Price detal between consicutive positions
                        strangleNode.OptionMatrix[i] = optionMatix[i];
                    }

                    return;
                }

                strangleNode.UnBookedPnL = 0;
                strangleNode.BookedPnL = 0;

                UpdateMatrix(callUniverse, ref optionMatix[CE]);
                UpdateMatrix(putUniverse, ref optionMatix[PE]);

                ///Step 2: Higher value strangle has not reached within 100 points to Binstruments -  Delta threshold
                ///TODO: MOVE THE TRADE AND NOT JUST CLOSE IT
                ///16-04-20: This step has been moved out as new ATM options are checked regularly to avoid Gamma play.
                int lotSize = GetLotSize();
                decimal currentPutQty = GetQtyInTrade(optionMatix[PE]);
                decimal currentCallQty = GetQtyInTrade(optionMatix[CE]);
                decimal currentQty = currentCallQty + currentPutQty;
                decimal qtyAvailable = strangleNode.MaxQty * lotSize - currentQty;

                //Stop trade when premium reaches lower level
                _stopTrade = StopTrade(ref optionMatix, strangleNode, timeOfOrder, Convert.ToInt32(qtyAvailable));
                if (_stopTrade)
                {
                    return;
                }
                CloseNearATMTrades(ref optionMatix, strangleNode, timeOfOrder, Convert.ToInt32(qtyAvailable));

                
                currentPutQty = GetQtyInTrade(optionMatix[PE]);
                currentCallQty = GetQtyInTrade(optionMatix[CE]);
                currentQty = currentCallQty + currentPutQty;
                qtyAvailable = strangleNode.MaxQty * lotSize - currentQty;


                decimal callInitialValue = GetMatrixInitialValue(optionMatix[CE]);
                decimal putInitialValue = GetMatrixInitialValue(optionMatix[PE]);

                decimal callValue = GetMatrixCurrentValue(optionMatix[CE]);
                decimal putValue = GetMatrixCurrentValue(optionMatix[PE]);
                decimal higherOptionValue = Math.Max(callValue, putValue);
                decimal lowerOptionValue = Math.Min(callValue, putValue);
                int highValueOptionType = callValue > putValue ? CE : PE;

                decimal initialStrangleValue = callInitialValue + putInitialValue; // only for trades that are not closed yet. Open strangle value.
                decimal currentStrangleValue = callValue + putValue;

                //Step: Check RSI and WMA on RSI and then take trade only if RSI is below 20 WMA RSI and RSI is below 55. Else manage one side.


                ///step:if next strike cross this strike on opposide side
                #region Manage one side
                if (higherOptionValue > lowerOptionValue * 1.5m)
                {
                    int stepQty = strangleNode.StepQty * lotSize;
                    

                    decimal maxQty = strangleNode.MaxQty * lotSize;
                    decimal maxQtyForStrangle = strangleNode.MaxQty * lotSize; // There can be 2 max qtys, a lower one for strangle and higher for side balancing
                    decimal bInstrumentPrice = strangleNode.BaseInstrumentPrice;
                    //decimal lotSize = strangleNode.CallUniverse.First().Value.LotSize;
                    decimal optionToken = 0;

                    //decimal premiumNeeded = (higherOptionValue - lowerOptionValue) / stepQty;
                    decimal premiumNeeded = (higherOptionValue - lowerOptionValue);

                    ///Step 1: Check if there are any profitable calltrades that can be bought back
                    CloseProfitableTrades(ref optionMatix[highValueOptionType], strangleNode, timeOfOrder, premiumNeeded, lotSize, highValueOptionType);

                    /////Step 2: Higher value strangle has not reached within 100 points to Binstruments -  Delta threshold
                    /////TODO: MOVE THE TRADE AND NOT JUST CLOSE IT
                    //CloseNearATMTrades(optionMatix[highValueOptionType], strangleNode, timeOfOrder, (InstrumentType)highValueOptionType);

                    ///Step 3: Check to see if trade can be taken out from lower value strangle matrix
                    ///TODO: This step may not be needed as lower value option gets moved to maintain delta. This should be a totally seperate step.
                    BookProfitAndMoveClosure();

                    ///Intermediate Step
                    ///Recheck value of options to see if further continuation is needed.
                    higherOptionValue = highValueOptionType == CE ? GetMatrixCurrentValue(optionMatix[CE]) : GetMatrixCurrentValue(optionMatix[PE]);

                    if (higherOptionValue <= lowerOptionValue * 1.5m)
                    {
                        return;
                    }

                    //Step 3: keep checking on the next in the money option and see if the value ot next strike price of opposite side crosses this option price

                    ///Step4: Look for increasing quantity on lower strike prices to increase the value of strangle just above loss making high value strangle
                    currentPutQty = GetQtyInTrade(optionMatix[PE]);
                    currentCallQty = GetQtyInTrade(optionMatix[CE]);
                    currentQty = currentCallQty + currentPutQty;
                    qtyAvailable = maxQty - currentQty;
                    decimal valueNeeded = 0;
                    decimal[,] matrix;

                    if (qtyAvailable >= lotSize)
                    {
                        valueNeeded = Math.Abs(higherOptionValue - lowerOptionValue);
                        matrix = callValue > putValue ? optionMatix[PE] : optionMatix[CE];


                        SortMatrixByPrice(ref matrix);

                        for (int i = 0; i < matrix.GetLength(0); i++)
                        {
                            if (matrix[i, TRADING_STATUS] == (decimal)TradeStatus.Open && (matrix[i, CURRENT_PRICE] * qtyAvailable >= valueNeeded || i == matrix.GetLength(0) - 1)) //2: CurrentPrice
                            {
                                string tradingSymbol = callValue > putValue ? strangleNode.PutUniverse[matrix[i, STRIKE]].TradingSymbol : strangleNode.CallUniverse[matrix[i, STRIKE]].TradingSymbol;
                                string instrumentType = callValue > putValue ? "PE" : "CE";
                                //Book this trade and update matix with only open trades
                                int qtyToBeBooked = Convert.ToInt32(Math.Ceiling((valueNeeded / matrix[i, CURRENT_PRICE]) / lotSize) * lotSize);


                                optionToken = matrix[i, INSTRUMENT_TOKEN];
                                //Instrument option = strangleNode.TradedStrangle.Options.First(x => x.InstrumentToken == optionToken);
                                Order order = MarketOrders.PlaceOrder(strangleNode.ID, tradingSymbol, instrumentType,
                                    matrix[i, CURRENT_PRICE], Convert.ToUInt32(optionToken),
                                    false, Convert.ToInt32(Math.Min(qtyToBeBooked, qtyAvailable)), algoIndex, timeOfOrder, Tag: strangleNode.ID.ToString());

                                OnTradeEntry(order);
                                decimal[,] data = new decimal[1, 10];
                                data[0, INSTRUMENT_TOKEN] = optionToken;
                                data[0, INITIAL_TRADED_PRICE] = order.AveragePrice;
                                data[0, CURRENT_PRICE] = order.AveragePrice;
                                data[0, QUANTITY] = order.Quantity;
                                data[0, TRADING_STATUS] = (decimal)TradeStatus.Open;
                                data[0, TRADE_ID] = strangleNode.ID;
                                data[0, POSITION_PnL] = 0;// PnL of trade as price updates
                                data[0, STRIKE] = matrix[i, STRIKE];// Strike Price of Instrument

                                //check the universe for appropriate strike price for addition. The price should be such that 1 step qty should help
                                //break;

                                if (callValue > putValue)
                                {
                                    strangleNode.AddMatrixRow(data, InstrumentType.PE);
                                }
                                else
                                {
                                    strangleNode.AddMatrixRow(data, InstrumentType.CE);
                                }
                                return;
                            }
                        }


                    }

                    ///Step 5: If no additional quantity available in Step 4, then move the strangles
                    MoveNearTerm(strangleNode, ref optionMatix, timeOfOrder);
                }
                #endregion
                #region Manage at strangle level
                else if (currentStrangleValue > 1.2m * initialStrangleValue)
                {
                    //int stepQty = strangleNode.StepQty;
                    //decimal maxQty = strangleNode.MaxQty;
                    //decimal maxQtyForStrangle = strangleNode.MaxQty; // There can be 2 max qtys, a lower one for strangle and higher for side balancing
                    //decimal bInstrumentPrice = strangleNode.BaseInstrumentPrice;
                    //decimal lotSize = strangleNode.CallUniverse.First().Value.LotSize;
                    //decimal optionStrike = 0;

                    ////Determine proper structure with minimum trade and first taking profit out from existing trade

                    //// Step 1: Check if max quantity is reached. If yes move to step 2 , else increase qty on both sides and balance
                    //currentQty = GetQtyInTrade(optionMatix[CE]) + GetQtyInTrade(optionMatix[PE]);
                    //maxQtyForStrangle = maxQty - currentQty;

                    //if (maxQtyForStrangle >= (stepQty * 2)) // Add qtys to near ATM options
                    //{
                    //    optionStrike = GetMinStrike(optionMatix[CE]);

                    //    string tradingSymbol = strangleNode.CallUniverse[optionStrike].TradingSymbol;
                    //    uint optionToken = strangleNode.CallUniverse[optionStrike].InstrumentToken;

                    //    //Instrument option = strangleNode.TradedStrangle.Options.First(x => x.InstrumentToken == optionToken);

                    //    ShortTrade shortTrade = PlaceOrder(strangleNode.ID,tradingSymbol, optionMatix[CE][0, CURRENT_PRICE],
                    //        optionToken, false, stepQty, timeOfOrder); //TODO: What qtys should be added to both side? same as original or same as step qty?

                    //    decimal[,] data = new decimal[1, 10];
                    //    data[0, INSTRUMENT_TOKEN] = optionToken;
                    //    data[0, INITIAL_TRADED_PRICE] = shortTrade.AveragePrice;
                    //    data[0, CURRENT_PRICE] = shortTrade.AveragePrice;
                    //    data[0, QUANTITY] = shortTrade.Quantity;
                    //    data[0, TRADING_STATUS] = (decimal)TradeStatus.Open;
                    //    data[0, TRADE_ID] = shortTrade.TriggerID;
                    //    data[0, POSITION_PnL] = 0;// PnL of trade as price updates
                    //    data[0, STRIKE] = optionStrike;// Strike Price of Instrument
                    //    strangleNode.AddMatrixRow(data, InstrumentType.CE);

                    //    optionStrike = GetMaxStrike(optionMatix[PE]);
                    //    tradingSymbol = strangleNode.PutUniverse[optionStrike].TradingSymbol;
                    //    optionToken = strangleNode.PutUniverse[optionStrike].InstrumentToken;

                    //    shortTrade = PlaceOrder(strangleNode.ID, tradingSymbol, optionMatix[PE][0, CURRENT_PRICE],
                    //       optionToken, false, stepQty, timeOfOrder);

                    //    data = new decimal[1, 10];
                    //    data[0, INSTRUMENT_TOKEN] = optionToken;
                    //    data[0, INITIAL_TRADED_PRICE] = shortTrade.AveragePrice;
                    //    data[0, CURRENT_PRICE] = shortTrade.AveragePrice;
                    //    data[0, QUANTITY] = shortTrade.Quantity;
                    //    data[0, TRADING_STATUS] = (decimal)TradeStatus.Open;
                    //    data[0, TRADE_ID] = shortTrade.TriggerID;
                    //    data[0, POSITION_PnL] = 0;// PnL of trade as price updates
                    //    data[0, STRIKE] = optionStrike;// Strike Price of Instrument
                    //    strangleNode.AddMatrixRow(data, InstrumentType.PE);

                    //}
                    ////Step 2: move far OTMs to near ATMS. Same as step 5 below
                    //else if (currentQty < maxQtyForStrangle)
                    //{
                    //    //optionToken = GetOptionWithMaxStrike(callMatix);
                    //    //Instrument option = strangleNode.TradedStrangle.Options.First(x => x.InstrumentToken == optionToken);
                    //    //PlaceOrder(strangleNode.ID, option, false, stepQty, timeOfOrder, triggerID: tradeCounter + 1);

                    //    //optionToken = GetOptionWithMinStrike(putMatix);
                    //    //option = strangleNode.TradedStrangle.Options.First(x => x.InstrumentToken == optionToken);
                    //    //PlaceOrder(strangleNode.ID, option, false, stepQty, timeOfOrder, triggerID: tradeCounter + 1);
                    //}

                }
                #endregion
                //Book this trade and update matix with only open trades
                strangleNode.OptionMatrix[CE] = optionMatix[CE];
                strangleNode.OptionMatrix[PE] = optionMatix[PE];
                Interlocked.Increment(ref _healthCounter);
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, tick.Timestamp.GetValueOrDefault(DateTime.UtcNow),
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "ManageStrangle");
                Thread.Sleep(100);
                Environment.Exit(0);
            }
        }

        /// <summary>
        /// Updates Options matrix with closed profitable trades
        /// </summary>
        /// <param name="optionMatrix"></param>
        /// <param name="strangleNode"></param>
        /// <param name="tradeQty"></param>
        /// <param name="timeOfOrder"></param>
        private void CloseProfitableTrades(ref decimal[,] optionMatrix, StrangleDataStructure strangleNode, 
            DateTime? timeOfOrder, decimal valueNeeded, decimal lotSize, int optionType)
        {
            try
            {
                SortedList<decimal, Instrument> optionUniverse = optionType == CE ? strangleNode.CallUniverse : strangleNode.PutUniverse;
                int closeCounter = 0;
                for (int i = 0; i < optionMatrix.GetLength(0); i++)
                {
                    if (optionMatrix[i, POSITION_PnL] > 0 && optionMatrix[i, POSITION_PnL] * lotSize <= valueNeeded && optionMatrix[i, TRADING_STATUS] == (decimal)TradeStatus.Open)
                    {
                        decimal optionValue = optionMatrix[i, POSITION_PnL] * optionMatrix[i, QUANTITY];
                        decimal qtyToBeClosed = Math.Round(valueNeeded / optionMatrix[i, CURRENT_PRICE] / lotSize) * lotSize;

                        decimal quantity = Math.Min(qtyToBeClosed, optionMatrix[i, QUANTITY]);
                        uint instrumentToken = Convert.ToUInt32(optionMatrix[i, INSTRUMENT_TOKEN]);
                        Instrument option = optionUniverse.FirstOrDefault(x => x.Value.InstrumentToken == instrumentToken).Value;

                        //ShortTrade trade = MarketOrders.PlaceOrder(strangleNode.ID, option, true, Convert.ToInt32(quantity), timeOfOrder, triggerID: i);

                        Order order = MarketOrders.PlaceOrder(strangleNode.ID, option.TradingSymbol, option.InstrumentType,
                            optionMatrix[i, CURRENT_PRICE], instrumentToken, true, Convert.ToInt32(quantity), algoIndex, timeOfOrder, Tag: strangleNode.ID.ToString());

                        OnTradeEntry(order);
                        optionMatrix[i, CURRENT_PRICE] = order.AveragePrice;

                        if (quantity == optionMatrix[i, QUANTITY])
                        {
                            optionMatrix[i, TRADING_STATUS] = (decimal)TradeStatus.Closed;
                            closeCounter++;
                        }
                        else
                        {
                            optionMatrix[i, TRADING_STATUS] = (decimal)TradeStatus.Open;
                        }
                        optionMatrix[i, QUANTITY] = optionMatrix[i, QUANTITY] - quantity;
                        valueNeeded -= optionMatrix[i, CURRENT_PRICE] * quantity;
                    }
                }

                if (closeCounter > 0)
                {
                    decimal[,] result = new decimal[optionMatrix.GetLength(0) - closeCounter, optionMatrix.GetLength(1)];

                    for (int i = 0, j = 0; i < optionMatrix.GetLength(0); i++)
                    {
                        if (optionMatrix[i, TRADING_STATUS] == (decimal)TradeStatus.Closed)
                        {
                            continue;
                        }
                        else
                        {
                            for (int k = 0, u = 0; k < optionMatrix.GetLength(1); k++)
                            {
                                result[j, u] = optionMatrix[i, k];
                                u++;
                            }
                        }
                        j++;
                    }
                    optionMatrix = result;
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(ex.Message);
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, timeOfOrder.GetValueOrDefault(DateTime.UtcNow),
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "ManageStrangle");
                Thread.Sleep(100);
                Environment.Exit(0);
            }
        }
        public static decimal[,] TrimArray(int rowToRemove, decimal[,] originalArray)
        {
            decimal[,] result = new decimal[originalArray.GetLength(0) - 1, originalArray.GetLength(1)];

            for (int i = 0, j = 0; i < originalArray.GetLength(0); i++)
            {
                if (i == rowToRemove)
                    continue;

                for (int k = 0, u = 0; k < originalArray.GetLength(1); k++)
                {
                    result[j, u] = originalArray[i, k];
                    u++;
                }
                j++;
            }

            return result;
        }

        private bool StopTrade(ref decimal[][,] optionMatrices, StrangleDataStructure strangleNode, DateTime? timeOfOrder, int quantityAvailable)
        {
            bool stopTrade = true;
            decimal minPrice = strangleNode.MinPremiumToTrade;
            for (int j = 0; j < optionMatrices.GetLength(0); j++)
            {
                decimal[,] optionMatrix = optionMatrices[j];
                for (int i = 0; i < optionMatrix.GetLength(0); i++)
                {
                    if (optionMatrix[i, CURRENT_PRICE] > minPrice)
                    {
                        stopTrade = false;
                        break;
                    }
                }
            }
            return stopTrade;
        }
        private void CloseNearATMTrades(ref decimal[][,] optionMatrices, StrangleDataStructure strangleNode, DateTime? timeOfOrder, int quantityAvailable)
        {
            for (int j = 0; j < optionMatrices.GetLength(0); j++)
            {
                int closeCounter = 0;
                decimal[,] optionMatrix = optionMatrices[j];
                InstrumentType optionType = j == CE ? InstrumentType.CE : InstrumentType.PE;

                for (int i = 0; i < optionMatrix.GetLength(0); i++)
                {
                    if (optionMatrix[i, STRIKE] > 0 && optionMatrix[i, TRADING_STATUS] == (decimal)TradeStatus.Open)
                    //strike price increment/ Manage Delta
                    {
                        //close this trade
                        int quantity = Convert.ToInt32(optionMatrix[i, QUANTITY]);
                        decimal strike = optionMatrix[i, STRIKE];
                        ///TODO: Need to move option 1 strike behind
                        if (optionType == InstrumentType.CE && strike <= strangleNode.BaseInstrumentPrice + strangleNode.MinDistanceFromBInstrument * 0.6m)
                        {
                            Instrument option = strangleNode.CallUniverse[strike];
                            //ShortTrade trade = PlaceOrder(strangleNode.ID, option.TradingSymbol, optionMatrix[i, CURRENT_PRICE], option.InstrumentToken, true, quantity, timeOfOrder, triggerID: i);
                            Order order = MarketOrders.PlaceOrder(strangleNode.ID, option.TradingSymbol, option.InstrumentType, optionMatrix[i, CURRENT_PRICE],
                                option.InstrumentToken, true, quantity, algoIndex, timeOfOrder, Tag: strangleNode.ID.ToString());

                            OnTradeEntry(order);
                            optionMatrix[i, CURRENT_PRICE] = order.AveragePrice;
                            optionMatrix[i, POSITION_PnL] = (optionMatrix[i, INITIAL_TRADED_PRICE] - order.AveragePrice) * quantity;
                            optionMatrix[i, TRADING_STATUS] = (decimal)TradeStatus.Closed;
                            closeCounter++;

                            SortedList<decimal, Instrument> callUniverse = strangleNode.CallUniverse;
                            KeyValuePair<decimal, Instrument> callNode = callUniverse.
                                Where(x => x.Key > strike && x.Key >= strangleNode.BaseInstrumentPrice + strangleNode.MinDistanceFromBInstrument).OrderBy(x => x.Key).First();

                            int oldQty = quantity;
                            decimal oldCurrentPrice = optionMatrix[i, CURRENT_PRICE];
                            decimal oldInitialTradedPrice = optionMatrix[i, INITIAL_TRADED_PRICE];

                            quantityAvailable = quantityAvailable + quantity;
                            quantity = Convert.ToInt32(optionMatrix[i, CURRENT_PRICE] / callNode.Value.LastPrice) * quantity;
                            quantity = quantity < quantityAvailable ? quantity : quantityAvailable;

                            PlaceOrderAndUpdateMatrix(strangleNode.ID, ref optionMatrix, callNode.Value, false, quantity, timeOfOrder, triggerID: i);
                            //strangleNode.OptionMatrix[CE] = optionMatrix; This is getting assigned in the main function, so no need here

                            //Loss Booked.
                            decimal lossBooked = (oldQty * oldCurrentPrice) - quantity * optionMatrix[optionMatrix.GetLength(0) - 1, CURRENT_PRICE];
                            if (lossBooked > 0)
                            {
                                decimal[,] oppositeMatrix = optionMatrices[j == CE ? PE : CE];
                                BookProfitFromStrangleNode(lossBooked, strangleNode, optionType == InstrumentType.CE ? InstrumentType.PE : InstrumentType.CE, ref oppositeMatrix, timeOfOrder);
                            }

                        }
                        else if (optionType == InstrumentType.PE && strike >= strangleNode.BaseInstrumentPrice - strangleNode.MinDistanceFromBInstrument * 0.6m)
                        {
                            Instrument option = strangleNode.PutUniverse[strike];
                            //ShortTrade trade = PlaceOrder(strangleNode.ID, option.TradingSymbol, optionMatrix[i, CURRENT_PRICE], option.InstrumentToken, true, quantity, timeOfOrder, triggerID: i);
                            Order order = MarketOrders.PlaceOrder(strangleNode.ID, option.TradingSymbol, option.InstrumentType, optionMatrix[i, CURRENT_PRICE],
                                option.InstrumentToken, true, quantity, algoIndex, timeOfOrder, Tag: strangleNode.ID.ToString());

                            OnTradeEntry(order);
                            optionMatrix[i, CURRENT_PRICE] = order.AveragePrice;
                            optionMatrix[i, TRADING_STATUS] = (decimal)TradeStatus.Closed;
                            closeCounter++;

                            int oldQty = quantity;
                            decimal oldCurrentPrice = optionMatrix[i, CURRENT_PRICE];
                            decimal oldInitialTradedPrice = optionMatrix[i, INITIAL_TRADED_PRICE];

                            SortedList<decimal, Instrument> putUniverse = strangleNode.PutUniverse;
                            KeyValuePair<decimal, Instrument> putNode = putUniverse.
                                 Where(x => x.Key < strike && x.Key <= strangleNode.BaseInstrumentPrice - strangleNode.MinDistanceFromBInstrument).OrderByDescending(x => x.Key).First();

                            quantityAvailable = quantityAvailable + quantity;
                            quantity = Convert.ToInt32(optionMatrix[i, CURRENT_PRICE] / putNode.Value.LastPrice) * quantity;
                            quantity = quantity < quantityAvailable ? quantity : quantityAvailable;

                            PlaceOrderAndUpdateMatrix(strangleNode.ID, ref optionMatrix, putNode.Value, false, quantity, timeOfOrder, triggerID: i);
                            //strangleNode.OptionMatrix[PE] = optionMatrix; This is getting assigned in the main function, so no need here

                            //Loss Booked.
                            decimal lossBooked = (oldQty * oldCurrentPrice) - quantity * optionMatrix[optionMatrix.GetLength(0) - 1, CURRENT_PRICE];
                            if (lossBooked > 0)
                            {
                                decimal[,] oppositeMatrix = optionMatrices[j == CE ? PE : CE];
                                BookProfitFromStrangleNode(lossBooked, strangleNode, optionType == InstrumentType.CE ? InstrumentType.PE : InstrumentType.CE, ref oppositeMatrix, timeOfOrder);
                            }
                        }
                    }
                }

                if (closeCounter > 0)
                {
                    decimal[,] result = new decimal[optionMatrix.GetLength(0) - closeCounter, optionMatrix.GetLength(1)];

                    for (int i = 0, w = 0; i < optionMatrix.GetLength(0); i++)
                    {
                        if (optionMatrix[i, TRADING_STATUS] == (decimal)TradeStatus.Closed)
                        {
                            continue;
                        }
                        else
                        {
                            for (int k = 0, u = 0; k < optionMatrix.GetLength(1); k++)
                            {
                                result[w, u] = optionMatrix[i, k];
                                u++;
                            }
                        }
                        w++;
                    }
                    optionMatrix = result;

                    optionMatrices[j] = optionMatrix;
                }
            }
        }
        
        private void BookProfitFromStrangleNode(decimal profitTobeBooked, StrangleDataStructure strangleNode, 
            InstrumentType optionType, ref decimal[,] optionMatrix, DateTime? timeOfOrder)
        {
            try
            {
                SortedList<decimal, Instrument> optionUniverse = optionType == CE ? strangleNode.CallUniverse : strangleNode.PutUniverse;
                SortMatrixByPrice(ref optionMatrix);
                int lotSize = GetLotSize(); //Corrected it to step qty as all trades should take at step qty level only. On second thought this will create problem if initial trade is not a multiple of stepqty
                int closeCounter = 0;
                
                for (int i = 0; i < optionMatrix.GetLength(0); i++)
                {
                    if (optionMatrix[i, POSITION_PnL] > 0 && optionMatrix[i, POSITION_PnL] * lotSize <= profitTobeBooked && optionMatrix[i, TRADING_STATUS] == (decimal)TradeStatus.Open)
                    {
                        decimal optionValue = optionMatrix[i, POSITION_PnL] * optionMatrix[i, QUANTITY];
                        decimal qtyToBeClosed = Math.Ceiling(profitTobeBooked / optionMatrix[i, CURRENT_PRICE] / lotSize) * lotSize;

                        decimal quantity = Math.Min(qtyToBeClosed, optionMatrix[i, QUANTITY]);
                        uint instrumentToken = Convert.ToUInt32(optionMatrix[i, INSTRUMENT_TOKEN]);
                        Instrument option = optionUniverse.FirstOrDefault(x => x.Value.InstrumentToken == instrumentToken).Value;

                        //ShortTrade trade = MarketOrders.PlaceOrder(strangleNode.ID, option, true, Convert.ToInt32(quantity), timeOfOrder, triggerID: i);

                        Order order = MarketOrders.PlaceOrder(strangleNode.ID, option.TradingSymbol, option.InstrumentType,
                            optionMatrix[i, CURRENT_PRICE], instrumentToken, true, Convert.ToInt32(quantity), algoIndex, timeOfOrder, Tag: strangleNode.ID.ToString());

                        OnTradeEntry(order);
                        optionMatrix[i, CURRENT_PRICE] = order.AveragePrice;

                        if (quantity == optionMatrix[i, QUANTITY])
                        {
                            optionMatrix[i, TRADING_STATUS] = (decimal)TradeStatus.Closed;
                            closeCounter++;
                        }
                        else
                        {
                            optionMatrix[i, TRADING_STATUS] = (decimal)TradeStatus.Open;
                        }
                        optionMatrix[i, QUANTITY] = optionMatrix[i, QUANTITY] - quantity;
                        profitTobeBooked -= optionMatrix[i, CURRENT_PRICE] * quantity;
                    }
                }

                if (closeCounter > 0)
                {
                    decimal[,] result = new decimal[optionMatrix.GetLength(0) - closeCounter, optionMatrix.GetLength(1)];

                    for (int i = 0, j = 0; i < optionMatrix.GetLength(0); i++)
                    {
                        if (optionMatrix[i, TRADING_STATUS] == (decimal)TradeStatus.Closed)
                        {
                            continue;
                        }
                        else
                        {
                            for (int k = 0, u = 0; k < optionMatrix.GetLength(1); k++)
                            {
                                result[j, u] = optionMatrix[i, k];
                                u++;
                            }
                        }
                        j++;
                    }
                    optionMatrix = result;
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(ex.Message);
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, timeOfOrder.GetValueOrDefault(DateTime.UtcNow),
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "ManageStrangle");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }
        }
        private void BookProfitAndMoveClosure()
        {
            //for (int i = 0; i < putMatix.GetLength(0); i++)
            //{
            //    if(putMatix[i, 2] < 0.5m * putMatix[i, 1]) 
            //    {
            //        //Book this trade and update matix with only open trades

            //        //close this trade
            //        optionToken = callMatix[i, INSTRUMENT_TOKEN];
            //        Instrument option = strangleNode.TradedStrangle.Options.First(x => x.InstrumentToken == optionToken);
            //        PlaceOrder(strangleNode.ID, option, false, stepQty, timeOfOrder, triggerID: i);
            //        callMatix[i, TRADING_STATUS] = (decimal)TradeStatus.Closed;
            //    }
            //}
        }
        private decimal UpdateMatrix(SortedList<decimal, Instrument> optionUniverse, ref decimal[,] optionMatrix)
        {
            decimal unBookedPnl = 0;
            for (int i = 0; i < optionMatrix.GetLength(0); i++)
            {
                if (optionMatrix[i, TRADING_STATUS] == (decimal)TradeStatus.Open)
                {
                    Instrument option = optionUniverse[optionMatrix[i, STRIKE]];
                    if (optionMatrix[i, INSTRUMENT_TOKEN] != option.InstrumentToken)
                    {
                        throw new Exception("Incorrect Option");
                    }
                    optionMatrix[i, CURRENT_PRICE] = option.LastPrice; //CurrentPrice
                    optionMatrix[i, POSITION_PnL] = (optionMatrix[i, INITIAL_TRADED_PRICE] - optionMatrix[i, CURRENT_PRICE]) * optionMatrix[i, QUANTITY];// PnL of trade as price updates
                    optionMatrix[i, STRIKE] = option.Strike;// Strike Price of Instrument
                    //optionMatrix[i, PRICEDELTA] = putTrade[1];// Price detal between consicutive positions
                    unBookedPnl +=
                }
            }
        }
        private int GetLotSize()
        {
            return Convert.ToInt32(ActiveStrangles[_algoInstance].CallUniverse.First().Value.LotSize);
        }
        private decimal GetMatrixPnL(decimal[,] matrix)
        {
            decimal matrixPnL = 0;
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                if (matrix[i, TRADING_STATUS] == (decimal)TradeStatus.Open)
                    matrixPnL += matrix[i, POSITION_PnL];
            }
            return matrixPnL;
        }
        private decimal GetMatrixInitialValue(decimal[,] matrix)
        {
            decimal matrixPnL = 0;
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                if (matrix[i, TRADING_STATUS] == (decimal)TradeStatus.Open)
                    matrixPnL += matrix[i, INITIAL_TRADED_PRICE] * matrix[i, QUANTITY];
            }
            return matrixPnL;
        }
        private decimal GetMatrixCurrentValue(decimal[,] matrix)
        {
            decimal matrixPnL = 0;
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                if (matrix[i, TRADING_STATUS] == (decimal)TradeStatus.Open)
                    matrixPnL += matrix[i, CURRENT_PRICE] * matrix[i, QUANTITY];
            }
            return matrixPnL;
        }
        //private decimal GetOptionDelta(uint token)
        //{
        //    //
        //    //BS bs = new BS()
        //}

        //private decimal GetOptionDelta(uint token)
        //{

        //}


        private decimal GetQtyInTrade(decimal[,] matrix)
        {
            decimal currentQty = 0;
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                if (matrix[i, TRADING_STATUS] == (decimal)TradeStatus.Open)
                    currentQty += matrix[i, QUANTITY]; //3: Quantity
            }
            return currentQty;
        }
        private void SortMatrixByPrice(ref decimal[,] matrix)
        {
            decimal[] tempVector = new decimal[10];
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = i + 1; j < matrix.GetLength(0); j++)
                {
                    if (matrix[i, CURRENT_PRICE] > matrix[j, CURRENT_PRICE] )//&& matrix[j, CURRENT_PRICE] != 0)
                    {
                        for (int t = 0; t < matrix.GetLength(1); t++)
                        {
                            tempVector[t] = matrix[i, t];
                            matrix[i, t] = matrix[j, t];
                            matrix[j, t] = tempVector[t];
                        }
                    }
                }
            }
        }
        private void MoveNearTerm(StrangleDataStructure strangleNode, ref decimal[][,] optionMatrix, DateTime? timeOfTrade)
        {
            try
            {
                SortMatrixByPrice(ref optionMatrix[CE]);
                SortMatrixByPrice(ref optionMatrix[PE]);

                decimal callValue = GetMatrixCurrentValue(optionMatrix[CE]);
                decimal putValue = GetMatrixCurrentValue(optionMatrix[PE]);

                decimal currentPutQty = GetQtyInTrade(optionMatrix[PE]);
                decimal currentCallQty = GetQtyInTrade(optionMatrix[CE]);


                decimal higherOptionValue = callValue;
                decimal lowerOptionValue = putValue;
                int lowerValueOptionType = PE;
                decimal[,] lowerMatrix = optionMatrix[PE];
                SortedList<decimal, Instrument> lowerOptionUniverse = strangleNode.PutUniverse;
                decimal lowerOptionQty = currentPutQty;
                if (putValue > callValue)
                {
                    lowerValueOptionType = CE;
                    lowerMatrix = optionMatrix[CE];
                    lowerOptionUniverse = strangleNode.CallUniverse;
                    higherOptionValue = putValue;
                    lowerOptionValue = callValue;
                    lowerOptionQty = currentCallQty;
                }

                //TODO: Update callvalue and putvalue at every step above, so that we have updated value here
                decimal valueNeeded = Math.Abs(callValue - putValue);

                int strategyId = strangleNode.ID;
                decimal valueGain = 0;
                bool stoploop = false;
                Instrument option;
                //int nodeCount = 0;
                int startNode = 0;
                int endNode = 0;
                //Check if current lower matrix options can suffice
                int closeCounter = 0;
                ///TODO: GET ONLY ACTIVE OPTIONS HERE..CHECK FOR REMOVING RATHER THAN SETTING CLOSED STATUS
                if (lowerMatrix[lowerMatrix.GetLength(0) - 1, CURRENT_PRICE] * lowerOptionQty > higherOptionValue)
                {
                    //No need to new options from universe
                    closeCounter = 0;
                    GetNodesforValueGain(lowerMatrix, valueNeeded, out startNode, out endNode);

                    valueGain = 0;
                    //move all nodes less than J to I
                    for (int j = 0; j < endNode; j++)//TODO: take length of only those items that are open
                    {
                        if(lowerMatrix[j, TRADING_STATUS] == (decimal) TradeStatus.Closed)
                        {
                            continue;
                        }

                        option = lowerValueOptionType == CE ? strangleNode.CallUniverse.Values.First(x => x.InstrumentToken == lowerMatrix[j, INSTRUMENT_TOKEN])
                            : strangleNode.PutUniverse.Values.First(x => x.InstrumentToken == lowerMatrix[j, INSTRUMENT_TOKEN]);

                        Order order = MarketOrders.PlaceOrder(strangleNode.ID, option.TradingSymbol, option.InstrumentType, lowerMatrix[j, CURRENT_PRICE],
                            option.InstrumentToken, true, Convert.ToInt32(lowerMatrix[j, QUANTITY]), algoIndex,
                            timeOfTrade, Tag: strangleNode.ID.ToString());

                        OnTradeEntry(order);
                        lowerMatrix[j, TRADING_STATUS] = (decimal)TradeStatus.Closed;
                        lowerMatrix[j, CURRENT_PRICE] = order.AveragePrice;
                        closeCounter++;

                        option = lowerValueOptionType == CE ? strangleNode.CallUniverse.Values.First(x => x.InstrumentToken == lowerMatrix[startNode, INSTRUMENT_TOKEN])
                            : strangleNode.PutUniverse.Values.First(x => x.InstrumentToken == lowerMatrix[startNode, INSTRUMENT_TOKEN]);

                        PlaceOrderAndUpdateMatrix(strangleNode.ID, ref lowerMatrix, option, false, Convert.ToInt32(lowerMatrix[j, QUANTITY]), timeOfTrade, triggerID: lowerMatrix[j, TRADE_ID]);

                        //PlaceOrder(strangleNode.ID, option, false, Convert.ToInt32(lowerMatrix[j, QUANTITY]), timeOfTrade, triggerID: lowerMatrix[j, TRADE_ID]);
                        //lowerMatrix[startNode, TRADING_STATUS] = (decimal)TradeStatus.Open;
                        //lowerMatrix[startNode, CURRENT_PRICE] = order.AveragePrice;
                        valueGain += (lowerMatrix[startNode, CURRENT_PRICE] - lowerMatrix[j, CURRENT_PRICE]) * lowerMatrix[j, QUANTITY];
                    }

                    if (valueGain < valueNeeded) // Run the check between J and I..so understand if node from J can be shifted inbetween instead to moving all the way to i
                    {
                        valueNeeded -= valueGain; // remaining value
                        valueGain = 0;
                        for (int p = endNode; p <= startNode; p++)
                        {
                            valueGain = (lowerMatrix[p, CURRENT_PRICE] - lowerMatrix[endNode, CURRENT_PRICE]) * lowerMatrix[endNode, QUANTITY];
                            if (valueGain > valueNeeded)
                            {
                                option = lowerValueOptionType == CE ? strangleNode.CallUniverse.Values.First(x => x.InstrumentToken == lowerMatrix[endNode, INSTRUMENT_TOKEN])
                            : strangleNode.PutUniverse.Values.First(x => x.InstrumentToken == lowerMatrix[endNode, INSTRUMENT_TOKEN]);


                                //ShortTrade order = PlaceOrder(strangleNode.ID, option, true, Convert.ToInt32(lowerMatrix[endNode, QUANTITY]), timeOfTrade, triggerID: lowerMatrix[endNode, TRADE_ID]);
                                Order order = MarketOrders.PlaceOrder(strangleNode.ID, option.TradingSymbol, option.InstrumentType, lowerMatrix[p, CURRENT_PRICE],
                            option.InstrumentToken, true, Convert.ToInt32(lowerMatrix[endNode, QUANTITY]), algoIndex,
                            timeOfTrade, Tag: strangleNode.ID.ToString());

                                OnTradeEntry(order);

                                lowerMatrix[endNode, TRADING_STATUS] = (decimal)TradeStatus.Closed;
                                closeCounter++;
                                lowerMatrix[endNode, CURRENT_PRICE] = order.AveragePrice;

                                option = lowerValueOptionType == CE ? strangleNode.CallUniverse.Values.First(x => x.InstrumentToken == lowerMatrix[p, INSTRUMENT_TOKEN])
                           : strangleNode.PutUniverse.Values.First(x => x.InstrumentToken == lowerMatrix[p, INSTRUMENT_TOKEN]);

                                PlaceOrderAndUpdateMatrix(strangleNode.ID, ref lowerMatrix, option, false, Convert.ToInt32(lowerMatrix[endNode, QUANTITY]), timeOfTrade, triggerID: lowerMatrix[endNode, TRADE_ID]);

                                //PlaceOrder(strangleNode.ID, option, false, Convert.ToInt32(lowerMatrix[endNode, QUANTITY]), timeOfTrade, triggerID: lowerMatrix[endNode, TRADE_ID]);
                                //lowerMatrix[p, QUANTITY] += lowerMatrix[endNode, QUANTITY];
                                //lowerMatrix[p, TRADING_STATUS] = (decimal)TradeStatus.Open;
                                //lowerMatrix[p, CURRENT_PRICE] = order.AveragePrice;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    //options from universe are needed
                    //go through all the levels and see where total qty x price > value needed.
                    closeCounter = 0;
                    SortMatrixByPrice(ref lowerMatrix);
                    //List<KeyValuePair<decimal, Instrument>> lowerValueOptions = lowerOptionUniverse.Where(x => x.Value.LastPrice > lowerMatrix[lowerMatrix.GetLength(0) - 1, CURRENT_PRICE]).OrderBy(x => x.Value.LastPrice).ToList();
                    List<KeyValuePair<decimal, Instrument>> lowerValueOptions = lowerOptionUniverse.Where(x => x.Value.LastPrice > lowerMatrix[lowerMatrix.GetLength(0) - 1, CURRENT_PRICE] 
                    && ((x.Value.InstrumentType.Trim(' ').ToLower() == "ce" && x.Value.Strike >= (strangleNode.BaseInstrumentPrice + strangleNode.StrikePriceIncrement * 0.6m))
                            ||(x.Value.InstrumentType.Trim(' ').ToLower() == "pe" && x.Value.Strike <= (strangleNode.BaseInstrumentPrice - strangleNode.StrikePriceIncrement * 0.6m)))).OrderBy(x => x.Value.LastPrice).ToList();

                    decimal currentQty = currentCallQty + currentPutQty;
                    int lotSize = GetLotSize();
                    decimal qtyAvailable = strangleNode.MaxQty * lotSize - currentQty; //qtyavailable should be 0 here as all the qtys have been checked and taken out already

                    //close existing ones and get new ones

                    for (int i = 0; i < lowerValueOptions.Count; i++)
                    {
                        KeyValuePair<decimal, Instrument> lowerOption = lowerValueOptions.ElementAt(i);
                        if (lowerOption.Value.LastPrice * (qtyAvailable + lowerOptionQty) > higherOptionValue 
                            || (i == lowerValueOptions.Count - 1) //In case no options can cross the high value option, then just take the maximum possible.
                            )
                        {
                            decimal optionPrice = lowerOption.Value.LastPrice;
                            decimal qty = Math.Ceiling(higherOptionValue / optionPrice / lotSize) * lotSize;
                            //decimal qtyRollover = 0;
                            decimal remainingValue = 0;
                            decimal qty2 = 0;
                            bool optionFound = false;
                            int nodeCount = -1;
                            
                            remainingValue = higherOptionValue - qty * optionPrice;
                            if (remainingValue > 0)
                            {
                                for (int j = 0; j < lowerMatrix.GetLength(0); j++)
                                {
                                    if ((lowerOptionQty - qty) * lowerMatrix[j, CURRENT_PRICE] > remainingValue)
                                    {
                                        qty2 = Math.Ceiling(remainingValue / lowerMatrix[j, CURRENT_PRICE] / lotSize) * lotSize;
                                        optionFound = true;
                                        nodeCount = j;
                                        break;
                                    }
                                }
                                if (!optionFound)
                                {
                                    qty = Math.Ceiling(higherOptionValue / optionPrice / lotSize) * lotSize;
                                    qty2 = 0;
                                }
                            }
                            //if (nodeCount < 0)
                            //{
                            //    Instrument instrument = lowerOptionUniverse.Where(x => x.Value.InstrumentToken == lowerMatrix[nodeCount, INSTRUMENT_TOKEN]).First().Value;
                            //}

                            Order order;
                            for (int j = 0; j < lowerMatrix.GetLength(0); j++)
                            {
                                if(lowerMatrix[j, TRADING_STATUS] == (decimal)TradeStatus.Closed)
                                {
                                    continue;
                                }

                                Instrument instrument = lowerOptionUniverse.Values.FirstOrDefault(x => x.InstrumentToken == lowerMatrix[j, INSTRUMENT_TOKEN]);
                                if(instrument == null)
                                {
                                    lowerMatrix[j, TRADING_STATUS] = (decimal)TradeStatus.Closed;
                                    closeCounter++;
                                    continue;
                                }
                                int quantity = Convert.ToInt32(lowerMatrix[j, QUANTITY]);
                                if (j != nodeCount)
                                {
                                    //ShortTrade order1 = PlaceOrder(strangleNode.ID, instrument1, true, Convert.ToInt32(lowerMatrix[j, QUANTITY]), timeOfTrade, triggerID: lowerMatrix[j, TRADE_ID]);

                                    order = MarketOrders.PlaceOrder(strangleNode.ID, instrument.TradingSymbol, instrument.InstrumentType,
                                        lowerMatrix[j, CURRENT_PRICE], instrument.InstrumentToken, true, Convert.ToInt32(lowerMatrix[j, QUANTITY]),
                                        algoIndex, timeOfTrade, Tag: strangleNode.ID.ToString());

                                    OnTradeEntry(order);

                                    lowerMatrix[j, TRADING_STATUS] = (decimal)TradeStatus.Closed;
                                    closeCounter++;
                                    lowerMatrix[j, CURRENT_PRICE] = order.AveragePrice;
                                }
                                else
                                {
                                    if (lowerMatrix[j, QUANTITY] - qty2 > 0)
                                    {
                                        quantity = Convert.ToInt32(lowerMatrix[j, QUANTITY] - qty2);
                                        //ShortTrade order1 = PlaceOrder(strangleNode.ID, instrument1, true, quantity, timeOfTrade, triggerID: lowerMatrix[j, TRADE_ID]);

                                        order = MarketOrders.PlaceOrder(strangleNode.ID, instrument.TradingSymbol, instrument.InstrumentType, lowerMatrix[j, CURRENT_PRICE],
                                                      instrument.InstrumentToken, true, quantity, algoIndex,
                                                      timeOfTrade, Tag: strangleNode.ID.ToString());
                                        OnTradeEntry(order);

                                        lowerMatrix[j, TRADING_STATUS] = (decimal)TradeStatus.Open;
                                        lowerMatrix[j, CURRENT_PRICE] = order.AveragePrice;
                                        lowerMatrix[j, QUANTITY] = qty2;
                                    }
                                    else
                                    {
                                        //Instrument instrument = lowerOptionUniverse.Where(x => x.Value.InstrumentToken == lowerMatrix[nodeCount, INSTRUMENT_TOKEN]).First().Value;
                                        PlaceOrderAndUpdateMatrix(strangleNode.ID, ref lowerMatrix, instrument, false, qty2, timeOfTrade, triggerID: 1);
                                    }
                                }
                            }
                                //ShortTrade order = PlaceOrder(strangleNode.ID, instrument1, true, quantity, timeOfTrade, triggerID: lowerMatrix[j, TRADE_ID]);
                                //lowerMatrix[j, TRADING_STATUS] = (decimal)TradeStatus.Closed;
                                //lowerMatrix[j, CURRENT_PRICE] = order.AveragePrice;
                            //}


                            //for (int j = 0; j < lowerMatrix.GetLength(0); j++)
                            //{
                            //    Instrument instrument1 = lowerOptionUniverse.Values.First(x => x.InstrumentToken == lowerMatrix[j, INSTRUMENT_TOKEN]);
                            //    int quantity = Convert.ToInt32(lowerMatrix[j, QUANTITY]); ;
                            //    if(instrument1.InstrumentToken == instrument.InstrumentToken )
                            //    {
                            //        if (lowerMatrix[j, QUANTITY] - qty2 > 0)
                            //        {
                            //            quantity = Convert.ToInt32(lowerMatrix[j, QUANTITY] - qty2);
                            //            ShortTrade order1 = PlaceOrder(strangleNode.ID, instrument1, true, quantity, timeOfTrade, triggerID: lowerMatrix[j, TRADE_ID]);
                            //            lowerMatrix[j, TRADING_STATUS] = (decimal)TradeStatus.Closed;
                            //            lowerMatrix[j, CURRENT_PRICE] = order1.AveragePrice;
                            //            lowerMatrix[j, QUANTITY] = qty2;
                            //        }
                            //        else
                            //        {
                            //            PlaceOrderAndUpdateMatrix(strangleNode.ID, ref lowerMatrix, instrument, false, qty2, timeOfTrade, triggerID: 1);
                            //        }
                            //    }
                            //    ShortTrade order = PlaceOrder(strangleNode.ID, instrument1, true, quantity, timeOfTrade, triggerID: lowerMatrix[j, TRADE_ID]);
                            //    lowerMatrix[j, TRADING_STATUS] = (decimal)TradeStatus.Closed;
                            //    lowerMatrix[j, CURRENT_PRICE] = order.AveragePrice;
                            //}


                            PlaceOrderAndUpdateMatrix(strangleNode.ID, ref lowerMatrix, lowerOption.Value, false, qty, timeOfTrade, triggerID: 1);
                            //PlaceOrderAndUpdateMatrix(strangleNode.ID, ref lowerMatrix, instrument, false, qty2, timeOfTrade, triggerID: 1);


                            break;
                        }
                    }
                    //then apply in that and the previous one appropriate level.
                }


                if (closeCounter > 0)
                {
                    decimal[,] result = new decimal[lowerMatrix.GetLength(0) - closeCounter, lowerMatrix.GetLength(1)];

                    for (int i = 0, j = 0; i < lowerMatrix.GetLength(0); i++)
                    {
                        if (lowerMatrix[i, TRADING_STATUS] == (decimal)TradeStatus.Closed)
                        {
                            continue;
                        }
                        else
                        {
                            for (int k = 0, u = 0; k < lowerMatrix.GetLength(1); k++)
                            {
                                result[j, u] = lowerMatrix[i, k];
                                u++;
                            }
                        }
                        j++;
                    }
                    lowerMatrix = result;
                }

                if (callValue > putValue)
                {
                    optionMatrix[PE] = lowerMatrix;
                }
                else
                {
                    optionMatrix[CE] = lowerMatrix;
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(ex.Message);
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, timeOfTrade.GetValueOrDefault(DateTime.UtcNow),
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "MoveNearTerm");
                Thread.Sleep(100);
                Environment.Exit(0);
            }

        }

        private void GetNodesforValueGain(decimal[,] lowerMatrix, decimal valueNeeded, out int startNode, out int endNode)
        {
            decimal valueGain = 0;
            for (int i = 1; i < lowerMatrix.GetLength(0); i++)//TODO: take length of only those items that are open
            {
                valueGain = 0;
                for (int j = 0; j < i; j++)//TODO: take length of only those items that are open
                {
                    valueGain += (lowerMatrix[i, CURRENT_PRICE] - lowerMatrix[j, CURRENT_PRICE]) * lowerMatrix[j, QUANTITY]; //why is quantity being fixed and not fungible.. make partial trade in qty possible based on lotsize.
                    if (valueGain > valueNeeded)
                    {
                        startNode = i;
                        endNode = j;
                        return;
                    }
                }
            }
            throw new Exception("logic failed!!");
        }
        private decimal GetMaxStrike(decimal[,] matrix)
        {
            decimal instrumentToken = 0;
            decimal strike = 0;
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                if (/*matrix[i, TRADING_STATUS] == Convert.ToInt32(TradeStatus.Open) &&*/ (matrix[i, STRIKE] > strike || strike == 0))
                {
                    strike = matrix[i, STRIKE];
                    instrumentToken = matrix[i, INSTRUMENT_TOKEN];
                }
            }
            return strike;
        }
        private decimal GetMinStrike(decimal[,] matrix)
        {
            decimal instrumentToken = 0;
            decimal strike = 0;
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                if (/*matrix[i, TRADING_STATUS] == Convert.ToInt32(TradeStatus.Open) && */ (matrix[i, STRIKE] < strike || strike == 0))
                {
                    strike = matrix[i, STRIKE];
                    instrumentToken = matrix[i, INSTRUMENT_TOKEN];
                }
            }
            return strike;
        }
        private bool GetBaseInstrumentPrice(Tick tick, StrangleDataStructure strangleNode)
        {
            Tick baseInstrumentTick = tick.InstrumentToken == _bInstrumentToken ? tick : null;
            if (baseInstrumentTick != null && baseInstrumentTick.LastPrice != 0)  //(strangleNode.BaseInstrumentPrice == 0)// * callOption.LastPrice * putOption.LastPrice == 0)
            {
                strangleNode.BaseInstrumentPrice = baseInstrumentTick.LastPrice;
            }
            if (strangleNode.BaseInstrumentPrice == 0)
            {
                return false;
            }
            return true;
        }
        private decimal[][] InitialTrade(StrangleDataStructure strangleNode, Tick tick)
        {
            try
            {
                decimal baseInstrumentPrice = strangleNode.BaseInstrumentPrice;
                decimal DistanceFromBaseInstrumentPrice = 150;
                decimal minPrice = _minPremiumToTrade;
                decimal maxPrice = 1200;

                SortedList<decimal, Instrument> callUniverse = strangleNode.CallUniverse;
                SortedList<decimal, Instrument> putUniverse = strangleNode.PutUniverse;

                KeyValuePair<decimal, Instrument> callNode = callUniverse.
                    Where(x => x.Value.LastPrice > minPrice && x.Value.LastPrice < maxPrice
                    && x.Key > baseInstrumentPrice + DistanceFromBaseInstrumentPrice).OrderBy(x => x.Key).First();

                KeyValuePair<decimal, Instrument> putNode = putUniverse.
                    Where(x => x.Value.LastPrice > minPrice && x.Value.LastPrice < maxPrice
                    && x.Key < baseInstrumentPrice - DistanceFromBaseInstrumentPrice).OrderBy(x => x.Key).Last();

                Instrument call = callNode.Value;
                Instrument put = putNode.Value;

                strangleNode.UnBookedPnL = 0;
                strangleNode.BookedPnL = 0;

                //TradedStrangle tradedStrangle = strangleNode.TradedStrangle == null ? new TradedStrangle() : strangleNode.TradedStrangle;
                //tradedStrangle.Options.Add(call);
                //tradedStrangle.Options.Add(put);

                int callQty = 0; decimal callPrice = call.LastPrice; int putQty = 0; decimal putPrice = put.LastPrice;
                int lotSize = Convert.ToInt32(put.LotSize);
                if (callPrice >= putPrice)
                {
                    callQty = Convert.ToInt32(putPrice * strangleNode.InitialQty * lotSize / callPrice);
                    callQty = callQty - Convert.ToInt32(callQty % lotSize);
                    putQty = strangleNode.InitialQty * lotSize;
                }
                else
                {
                    putQty = Convert.ToInt32(callPrice * strangleNode.InitialQty * lotSize / putPrice);
                    putQty = putQty - Convert.ToInt32(putQty % lotSize);
                    callQty = strangleNode.InitialQty * lotSize;
                }

                int tradeId = 0;// tradedStrangle.SellTrades.Count;

                //ShortTrade callSellTrade = PlaceOrder(strangleNode.ID, call, buyOrder: false, callQty, tickTime: ticks[0].LastTradeTime, triggerID: tradeId);

                Order callSellOrder = MarketOrders.PlaceOrder(strangleNode.ID, call.TradingSymbol, call.InstrumentType, callPrice,
                          call.InstrumentToken, false, callQty, algoIndex,
                          tick.LastTradeTime, Tag: strangleNode.ID.ToString());


                //tradedStrangle.SellTrades.Add(callSellTrade);
                //ShortTrade putSellTrade = PlaceOrder(strangleNode.ID, put, buyOrder: false, putQty, tickTime: ticks[0].LastTradeTime, triggerID: tradeId);

                Order putSellOrder = MarketOrders.PlaceOrder(strangleNode.ID, put.TradingSymbol, put.InstrumentType, putPrice,
                          put.InstrumentToken, false, putQty, algoIndex,
                          tick.LastTradeTime, Tag: strangleNode.ID.ToString());

                //tradedStrangle.SellTrades.Add(putSellTrade);

                OnTradeEntry(callSellOrder);
                OnTradeEntry(putSellOrder);

                strangleNode.NetCallQtyInTrade += callSellOrder.Quantity + putSellOrder.Quantity;
                strangleNode.UnBookedPnL += (callSellOrder.Quantity * callSellOrder.AveragePrice + putSellOrder.Quantity * putSellOrder.AveragePrice);

                //tradedStrangle.UnbookedPnl = strangleNode.UnBookedPnL;
                //tradedStrangle.TradingStatus = PositionStatus.Open;

                //strangleNode.TradedStrangle = tradedStrangle;

                Decimal[][] tradeDetails = new decimal[2][];
                tradeDetails[CE] = new decimal[10];
                tradeDetails[CE][INSTRUMENT_TOKEN] = call.InstrumentToken;
                tradeDetails[CE][INITIAL_TRADED_PRICE] = callSellOrder.AveragePrice;
                tradeDetails[CE][CURRENT_PRICE] = callSellOrder.AveragePrice;
                tradeDetails[CE][QUANTITY] = callSellOrder.Quantity;
                tradeDetails[CE][TRADE_ID] = tradeId;
                tradeDetails[CE][TRADING_STATUS] = Convert.ToDecimal(TradeStatus.Open);
                tradeDetails[CE][POSITION_PnL] = 0;// callSellOrder.Quantity * callSellOrder.AveragePrice;
                tradeDetails[CE][STRIKE] = call.Strike;
                tradeDetails[PE] = new decimal[10];
                tradeDetails[PE][INSTRUMENT_TOKEN] = put.InstrumentToken;
                tradeDetails[PE][INITIAL_TRADED_PRICE] = putSellOrder.AveragePrice;
                tradeDetails[PE][CURRENT_PRICE] = putSellOrder.AveragePrice;
                tradeDetails[PE][QUANTITY] = putSellOrder.Quantity;
                tradeDetails[PE][TRADE_ID] = tradeId;
                tradeDetails[PE][TRADING_STATUS] = Convert.ToDecimal(TradeStatus.Open);
                tradeDetails[PE][POSITION_PnL] = 0;// putSellOrder.Quantity * putSellOrder.AveragePrice;
                tradeDetails[PE][STRIKE] = put.Strike;
                return tradeDetails;
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(ex.Message);
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, tick.Timestamp.GetValueOrDefault(DateTime.UtcNow),
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "InitialTrade");
                Thread.Sleep(100);
                Environment.Exit(0);
            }
            return null;
        }
        private void PlaceOrderAndUpdateMatrix(int strangleID, ref decimal[,] optionMatrix, Instrument instrument, 
            bool buyOrder, decimal quantity, DateTime? tickTime = null, uint token = 0, decimal triggerID = 0)
        {
            //ShortTrade trade = PlaceOrder(strangleID, instrument, buyOrder, Convert.ToInt32(quantity), tickTime, token, triggerID);
            token = token == 0 ? instrument.InstrumentToken : token;
            Order order = MarketOrders.PlaceOrder(strangleID, instrument.TradingSymbol, instrument.InstrumentType, instrument.LastPrice, // optionMatrix[optionMatrix.GetLength(0) - 1, CURRENT_PRICE],
                      token, buyOrder, Convert.ToInt32(quantity), algoIndex,
                      tickTime, Tag: strangleID.ToString());

            OnTradeEntry(order);
            Decimal[,] newMatrix = new decimal[optionMatrix.GetLength(0) + 1, optionMatrix.GetLength(1)];
            Array.Copy(optionMatrix, newMatrix, optionMatrix.Length - 1);

            newMatrix[newMatrix.GetLength(0) - 1, INSTRUMENT_TOKEN] = instrument.InstrumentToken;
            newMatrix[newMatrix.GetLength(0) - 1, INITIAL_TRADED_PRICE] = order.AveragePrice;
            newMatrix[newMatrix.GetLength(0) - 1, CURRENT_PRICE] = order.AveragePrice;
            newMatrix[newMatrix.GetLength(0) - 1, QUANTITY] = order.Quantity;
            newMatrix[newMatrix.GetLength(0) - 1, TRADE_ID] = triggerID;
            newMatrix[newMatrix.GetLength(0) - 1, TRADING_STATUS] = Convert.ToDecimal(TradeStatus.Open);
            newMatrix[newMatrix.GetLength(0) - 1, POSITION_PnL] = order.Quantity * order.AveragePrice;
            newMatrix[newMatrix.GetLength(0) - 1, STRIKE] = instrument.Strike;

            optionMatrix = newMatrix;
        }

        private decimal UpdateMaxPainStrike(StrangleDataStructure strangleNode, Tick tick)
        {
            //Check and Fill Reference Strangle Data
            //if (strikePrice == 0 || strikePrice != strangleNode.MaxPainStrike)
            //{
            bool allpopulated = PopulateReferenceStrangleData(strangleNode, tick);
            //}
            if (!allpopulated) return 0;

            SortedList<decimal, Instrument> calls = strangleNode.CallUniverse;
            SortedList<decimal, Instrument> puts = strangleNode.PutUniverse;

            IList<decimal> strikePrices = calls.Keys;
            decimal pain, maxPain = 0;
            decimal maxPainStrike = 0;
            foreach (decimal expiryStrike in strikePrices)
            {
                pain = 0;
                IEnumerable<Decimal> putStrikes = strikePrices.Where(x => x < expiryStrike);
                IEnumerable<Decimal> callStrikes = strikePrices.Where(x => x > expiryStrike);

                foreach (decimal strikePrice in putStrikes)
                {
                    pain += (puts[strikePrice].OI - puts[strikePrice].BeginingPeriodOI) * (expiryStrike - strikePrice);
                }
                foreach (decimal strikePrice in callStrikes)
                {
                    pain += (calls[strikePrice].OI - calls[strikePrice].BeginingPeriodOI) * (strikePrice - expiryStrike);
                }
                if (pain < maxPain || (maxPain == 0 && pain != 0))
                {
                    maxPain = pain;
                    maxPainStrike = expiryStrike;
                }
            }

            return maxPainStrike;
        }
        private bool PopulateReferenceStrangleData(StrangleDataStructure strangleNode, Tick tick, decimal includeStrike = 0)
        {
            try
            {
                decimal lowerband = strangleNode.BaseInstrumentPrice - strangleNode.StrikePriceIncrement * 2;
                decimal upperband = strangleNode.BaseInstrumentPrice + strangleNode.StrikePriceIncrement * 2;

                if (strangleNode.CallUniverse.Count == 0
                    || strangleNode.PutUniverse.Count == 0
                    || lowerband < strangleNode.CallUniverse.First().Value.Strike
                    || upperband > strangleNode.CallUniverse.Last().Value.Strike
                    || includeStrike != 0)
                {
                    SortedList<Decimal, Instrument>[] nodeData;
                    if (includeStrike == 0)
                    {
                        nodeData = GetNewStrikes(strangleNode.BaseInstrumentToken, strangleNode.BaseInstrumentPrice, strangleNode.Expiry);
                    }
                    else
                    {
                        nodeData = GetNewStrikes(strangleNode.BaseInstrumentToken, includeStrike, strangleNode.Expiry);//, strangleNode.StrikePriceIncrement);
                    }

                    //if (strangleNode.CallUniverse.Count > 0)
                    //{
                    //    foreach (KeyValuePair<decimal, Instrument> keyValuePair in nodeData[0])
                    //    {
                    //        if (!strangleNode.CallUniverse.ContainsKey(keyValuePair.Key))
                    //        {
                    //            strangleNode.CallUniverse.Add(keyValuePair.Key, keyValuePair.Value);

                    //            //if(!SubscriptionTokens.Contains(keyValuePair.Value.InstrumentToken))
                    //            //{
                    //            SubscriptionTokens.Add(keyValuePair.Value.InstrumentToken);
                    //            //}
                    //        }
                    //    }
                    //}
                    //else
                    //{
                    //    strangleNode.CallUniverse = nodeData[0];
                    //    SubscriptionTokens.AddRange(nodeData[0].Values.Select(x => x.InstrumentToken));
                    //}
                    foreach (KeyValuePair<decimal, Instrument> keyValuePair in nodeData[0])
                    {
                        if (!strangleNode.CallUniverse.ContainsKey(keyValuePair.Key))
                        {
                            strangleNode.CallUniverse.Add(keyValuePair.Key, keyValuePair.Value);

                            //if(!SubscriptionTokens.Contains(keyValuePair.Value.InstrumentToken))
                            //{
                            SubscriptionTokens.Add(keyValuePair.Value.InstrumentToken);
                            //}
                        }
                    }

                    //strangleNode.CallUniverse = nodeData[0];
                    //SubscriptionTokens.AddRange(nodeData[0].Values.Select(x => x.InstrumentToken));


                    // if (strangleNode.PutUniverse.Count > 0)
                    // {
                    foreach (KeyValuePair<decimal, Instrument> keyValuePair in nodeData[1])
                    {
                        if (!strangleNode.PutUniverse.ContainsKey(keyValuePair.Key))
                        {
                            strangleNode.PutUniverse.Add(keyValuePair.Key, keyValuePair.Value);

                            // if (!SubscriptionTokens.Contains(keyValuePair.Value.InstrumentToken))
                            // {
                            SubscriptionTokens.Add(keyValuePair.Value.InstrumentToken);
                            //}
                        }
                    }
                    //}
                    //else
                    //{
                    //  strangleNode.PutUniverse = nodeData[1];
                    //  SubscriptionTokens.AddRange(nodeData[1].Values.Select(x => x.InstrumentToken));
                    // }

                    ///All FILTERS REMOVED. CHECKING FOR SUBSCRIBED TOKENS AT THE LOCAL NODE
                    //DataAccess.IgniteConnector.QueryTickContinuous(SubscriptionTokens, this);
                    //OnOptionUniverseChange?.BeginInvoke(this,null, null);


                    Task task = Task.Run(() => OnOptionUniverseChange(this));

                    return false;
                }

                for (int i = 0; i < strangleNode.CallUniverse.Count; i++)
                {
                    Instrument instrument = strangleNode.CallUniverse.ElementAt(i).Value;
                    decimal strike = strangleNode.CallUniverse.ElementAt(i).Key;

                    //Tick optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == instrument.InstrumentToken);

                    if ( tick.InstrumentToken == instrument.InstrumentToken && tick.LastPrice != 0)
                    {
                        instrument.LastPrice = tick.LastPrice;                    
                        instrument.InstrumentType = "CE";
                        instrument.Bids = tick.Bids;
                        instrument.Offers = tick.Offers;
                        instrument.OI = tick.OI;
                        instrument.BeginingPeriodOI = instrument.BeginingPeriodOI == 0 ? instrument.OI : instrument.BeginingPeriodOI;
                        instrument.OIDayHigh = tick.OIDayHigh;
                        instrument.OIDayLow = tick.OIDayLow;
                        strangleNode.CallUniverse[strike] = instrument;
                    }
                }

                for (int i = 0; i < strangleNode.PutUniverse.Count; i++)
                {
                    Instrument instrument = strangleNode.PutUniverse.ElementAt(i).Value;
                    decimal strike = strangleNode.PutUniverse.ElementAt(i).Key;

                    //Tick optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == instrument.InstrumentToken);
                    //if (optionTick != null && optionTick.LastPrice != 0)
                    //{
                    if (tick.InstrumentToken == instrument.InstrumentToken && tick.LastPrice != 0)
                    {
                        instrument.LastPrice = tick.LastPrice;
                        instrument.InstrumentType = "PE";
                        instrument.Bids = tick.Bids;
                        instrument.Offers = tick.Offers;
                        instrument.OI = tick.OI;
                        instrument.BeginingPeriodOI = instrument.BeginingPeriodOI == 0 ? instrument.OI : instrument.BeginingPeriodOI;
                        instrument.OIDayHigh = tick.OIDayHigh;
                        instrument.OIDayLow = tick.OIDayLow;
                        strangleNode.PutUniverse[strike] = instrument;
                    }
                }

                return !Convert.ToBoolean(strangleNode.CallUniverse.Values.Count(x => x.LastPrice == 0) + strangleNode.PutUniverse.Values.Count(x => x.LastPrice == 0));
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(ex.Message);
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, tick.Timestamp.GetValueOrDefault(DateTime.UtcNow),
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "PopulateReferenceStrangleData");
                Thread.Sleep(100);
                Environment.Exit(0);
            }
            return false;
        }
        private void UpdateTokenSubcription ()
        {
            //ZMQClient.ZMQSubcribebyToken(this, SubscriptionTokens.ToArray());
        }
        public SortedList<Decimal, Instrument>[] GetNewStrikes(uint baseInstrumentToken, decimal baseInstrumentPrice, DateTime? expiry)
        {
            DataLogic dl = new DataLogic();
            SortedList<Decimal, Instrument>[] nodeData = dl.RetrieveNextStrangleNodes(baseInstrumentToken, expiry.GetValueOrDefault(DateTime.Now), 
                baseInstrumentPrice, baseInstrumentPrice, 0);
            return nodeData;
        }

        public Task<bool> OnNext(Tick[] ticks)
        {
            try
            {
                lock (ActiveStrangles)
                {
                    for (int i = 0; i < ActiveStrangles.Count; i++)
                    {
                        if (_stopTrade || !ticks[0].Timestamp.HasValue)
                        {
                            return Task.FromResult(false);
                        }
                        //ReviewStrangle(ActiveStrangles.ElementAt(i).Value, ticks);
                        ManageStrangle(ActiveStrangles.ElementAt(i).Value, ticks[0]);
                        return Task.FromResult(true);
                    }
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(ex.Message);
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, 
                    ticks[0].Timestamp.GetValueOrDefault(DateTime.UtcNow), String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "OnNext");
                Thread.Sleep(100);
                Environment.Exit(0);
                return Task.FromResult(false);
            }
            return Task.FromResult(true);
        }

        //public void OnEvent(IEnumerable<ICacheEntryEvent<TickKey, Tick>> events)
        //{
        //    //lock (ActiveStrangles)
        //    //{
        //    //    for (int i = 0; i < ActiveStrangles.Count; i++)
        //    //    {
        //    //        foreach (var e in events)
        //    //        {
        //    //            if(SubscriptionTokens.Contains(e.Value.InstrumentToken))
        //    //            ManageStrangle(ActiveStrangles.ElementAt(i).Value, new Tick[] { e.Value });
        //    //        }
        //    //    }
        //    //}
        //}
        //public bool Invoke(Guid nodeId, Tick tick)
        //{
        //    lock (ActiveStrangles)
        //    {
        //        for (int i = 0; i < ActiveStrangles.Count; i++)
        //        {
        //            //ReviewStrangle(ActiveStrangles.ElementAt(i).Value, ticks);
        //            ManageStrangle(ActiveStrangles.ElementAt(i).Value, new Tick[] { tick });

        //            //   foreach (KeyValuePair<int, StrangleDataList> keyValuePair in ActiveStrangles)
        //            // {
        //            //  ReviewStrangle(keyValuePair.Value, ticks);
        //            // }
        //        }
        //    }
        //    return true;
        //}

        //private ShortTrade PlaceOrder(int strangleID, string instrument_tradingsymbol, decimal instrument_currentPrice, uint instrument_Token, 
        //    bool buyOrder, int quantity, DateTime? tickTime = null, uint token = 0, decimal triggerID = 0)
        //{
        //    //quantity = 25;
        //    string tradingSymbol = instrument_tradingsymbol;
        //    decimal currentPrice = instrument_currentPrice;
        //    Dictionary<string, dynamic> orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, tradingSymbol.TrimEnd(),
        //                              buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL, quantity, Product: Constants.PRODUCT_MIS,
        //                              OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);

        //    /////TEMP, REMOVE Later
        //    //if (currentPrice == 0)
        //    //{
        //    //    DataLogic dl = new DataLogic();
        //    //    currentPrice = dl.RetrieveLastPrice(instrument_Token, tickTime, buyOrder);
        //    //}

        //    string orderId = "0";
        //    decimal averagePrice = 0;
        //    if (orderStatus != null && orderStatus["data"]["order_id"] != null)
        //    {
        //        orderId = orderStatus["data"]["order_id"];
        //    }
        //    if (orderId != "0")
        //    {
        //        System.Threading.Thread.Sleep(200);
        //        List<Order> orderInfo = ZObjects.kite.GetOrderHistory(orderId);
        //        averagePrice = orderInfo[orderInfo.Count - 1].AveragePrice;
        //    }
        //    //if (averagePrice == 0)
        //    //    averagePrice = buyOrder ? currentPrice : currentPrice;
        //    if(averagePrice == 0)
        //    {
        //        Logger.LogWrite("Error in placing order!");
        //        throw new Exception("Error in placing order!");
        //    }
        //    averagePrice = currentPrice;
        //    // averagePrice = buyOrder ? averagePrice * -1 : averagePrice;

        //    ShortTrade trade = new ShortTrade();
        //    trade.AveragePrice = averagePrice;
        //    trade.ExchangeTimestamp = tickTime;// DateTime.Now;
        //    trade.Quantity = quantity;
        //    trade.OrderId = orderId;
        //    trade.TransactionType = buyOrder ? "Buy" : "Sell";
        //    trade.TriggerID = Convert.ToInt32(triggerID);
        //    trade.TradingStatus = buyOrder ? TradeStatus.Closed : TradeStatus.Open;
        //    UpdateTradeDetails(strangleID, instrument_Token, quantity, trade, Convert.ToInt32(triggerID));

        //    return trade;
        //}
        //private ShortTrade PlaceOrder(int strangleID, Instrument instrument, bool buyOrder, int quantity, DateTime? tickTime = null, uint token = 0, decimal triggerID = 0)
        //{
        //    //quantity = 25;
        //    string tradingSymbol = instrument.TradingSymbol;
        //    decimal currentPrice = instrument.LastPrice;

        //    Dictionary<string, dynamic> orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, tradingSymbol.TrimEnd(),
        //                              buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL, quantity, Product: Constants.PRODUCT_MIS,
        //                              OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);


        //    /////TEMP, REMOVE Later
        //    //if (currentPrice == 0)
        //    //{
        //    //    DataLogic dl = new DataLogic();
        //    //    currentPrice = dl.RetrieveLastPrice(instrument.InstrumentToken, tickTime, buyOrder);
        //    //}

        //    string orderId = "0";
        //    decimal averagePrice = 0;
        //    if (orderStatus != null && orderStatus["data"]["order_id"] != null)
        //    {
        //        orderId = orderStatus["data"]["order_id"];
        //    }
        //    if (orderId != "0")
        //    {
        //        System.Threading.Thread.Sleep(200);
        //        List<Order> orderInfo = ZObjects.kite.GetOrderHistory(orderId);
        //        averagePrice = orderInfo[orderInfo.Count - 1].AveragePrice;
        //    }
        //    //TODO: if average price doesnt come back, then raise exception in life case
        //    if (averagePrice == 0)
        //    {
        //        Logger.LogWrite("Error in placing order!");
        //        //throw new Exception("Error in placing order!");
        //        Environment.Exit(0);
        //    }
        //    averagePrice = currentPrice;

        //    ShortTrade trade = new ShortTrade();
        //    trade.AveragePrice = averagePrice;
        //    trade.ExchangeTimestamp = tickTime;// DateTime.Now;
        //    trade.Quantity = quantity;
        //    trade.OrderId = orderId;
        //    trade.TransactionType = buyOrder ? "Buy" : "Sell";
        //    trade.TriggerID = Convert.ToInt32(triggerID);
        //    trade.TradingStatus = buyOrder ? TradeStatus.Closed : TradeStatus.Open;
        //    UpdateTradeDetails(strangleID, instrument.InstrumentToken, quantity, trade, Convert.ToInt32(triggerID));

        //    return trade;
        //}
        //private void UpdateTradeDetails(int strategyID, uint instrumentToken, int tradedLot, ShortTrade trade, int triggerID)
        //{
        //    DataLogic dl = new DataLogic();
        //    dl.UpdateTrade(strategyID, instrumentToken, trade, AlgoIndex.ExpiryTrade, tradedLot, triggerID);
        //}
        private void LoadActiveData()
        {
            AlgoIndex algoIndex = AlgoIndex.ExpiryTrade;
            DataLogic dl = new DataLogic();
            DataSet activeStrangles = dl.RetrieveActiveStrangleData(algoIndex);
            DataRelation strategy_Token_Relation = activeStrangles.Relations.Add("Strangle_Token", new DataColumn[] { activeStrangles.Tables[0].Columns["Id"] },
                new DataColumn[] { activeStrangles.Tables[1].Columns["StrategyId"] });

            DataRelation strategy_Trades_Relation = activeStrangles.Relations.Add("Strangle_Trades", new DataColumn[] { activeStrangles.Tables[0].Columns["Id"] },
                new DataColumn[] { activeStrangles.Tables[2].Columns["StrategyId"] });

            Instrument call, put;

            foreach (DataRow strangleRow in activeStrangles.Tables[0].Rows)
            {
                StrangleDataStructure strangleNode = new StrangleDataStructure();
                strangleNode.BaseInstrumentToken = Convert.ToUInt32(strangleRow["BToken"]);
                strangleNode.Expiry = Convert.ToDateTime(strangleRow["Expiry"]);
                strangleNode.MaxLossThreshold = Convert.ToDecimal(strangleRow["MaxLossPoints"]);
                strangleNode.ProfitTarget = Convert.ToDecimal(strangleRow["MaxProfitPoints"]);
                strangleNode.MaxQty = Convert.ToInt32(strangleRow["MaxQty"]);
                strangleNode.InitialQty = Convert.ToInt32(strangleRow["InitialQty"]);
                strangleNode.StepQty = Convert.ToInt32(strangleRow["StepQty"]);
                strangleNode.StrikePriceIncrement = Convert.ToInt32(strangleRow["StrikePriceIncrement"]);
                strangleNode.ID = Convert.ToInt32(strangleRow["ID"]);

                DataRow[] strangleTokenRows = strangleRow.GetChildRows(strategy_Token_Relation);
                if (strangleTokenRows.Count() == 0)
                {
                    ActiveStrangles.Add(strangleNode.ID, strangleNode);
                    continue;
                }

                ActiveStrangles.Add(strangleNode.ID, strangleNode);
            }
        }
        private void LoadActiveData(int algoInstance, uint bToken, DateTime expiry, int maxQty, 
            int initialQty, int stepQty, int strikePriceIncrement, int minDistanceFromBInstrument, decimal minPremiumToTrade)
        {
            StrangleDataStructure strangleNode = new StrangleDataStructure();
            strangleNode.BaseInstrumentToken = bToken;
            strangleNode.Expiry = expiry;
            strangleNode.MaxLossThreshold = 0;
            strangleNode.ProfitTarget = 0;
            strangleNode.MaxQty = maxQty;
            strangleNode.InitialQty = initialQty;
            strangleNode.StepQty = stepQty;
            strangleNode.StrikePriceIncrement = strikePriceIncrement;
            strangleNode.ID = algoInstance;
            strangleNode.MinDistanceFromBInstrument = minDistanceFromBInstrument;
            strangleNode.MinPremiumToTrade = minPremiumToTrade;

            ActiveStrangles.Add(strangleNode.ID, strangleNode);
        }

        public void StoreIndexForExpiryTrade(uint bToken, DateTime expiry, int tradingQty, int strikePriceIncrement, decimal maxLossThreshold, 
            decimal profitTarget, DateTime timeOfOrder = default(DateTime))
        {
            timeOfOrder = DateTime.Now;

            //Update Database
            DataLogic dl = new DataLogic();
            dl.StoreIndexForMainPainStrangle(bToken: bToken, expiry, strikePriceIncrement: strikePriceIncrement, algoIndex: AlgoIndex.ExpiryTrade, 
                tradingQty: tradingQty, maxLossThreshold, profitTarget, timeOfOrder);
        }

        private void _healthCheckTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            //expecting atleast 30 ticks in 1 min
            if (_healthCounter >= 30)
            {
                _healthCounter = 0;
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Health, e.SignalTime, "1", "CheckHealth");
            }
            else
            {
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Health, e.SignalTime, "0", "CheckHealth");
            }
        }

        private void __loggerTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            for (int i = 0; i < ActiveStrangles.Count; i++)
            {
                Decimal[][,] optionMatix = ActiveStrangles.ElementAt(i).Value.OptionMatrix;
                decimal callValue = optionMatix[CE] != null ? GetMatrixCurrentValue(optionMatix[CE]) : 0;
                decimal putValue = optionMatix[PE] != null ? GetMatrixCurrentValue(optionMatix[PE]) : 0;

                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.SignalTime, string.Format("Strangle Value. Call side: {0}  |  Put side: {1}", callValue, putValue) , "Logger");
            }
        }

        

        //private void CheckHealth(object sender, ElapsedEventArgs e)
        //{

        //}

        public virtual void OnError(Exception ex)
        {
            ///TODO: Log the error. Also handle the error.
        }

        //public virtual void Subscribe(Publisher publisher)
        //{
        //    UnsubscriptionToken = publisher.Subscribe(this);
        //}
        //public virtual void Subscribe(Ticker publisher)
        //{
        //    UnsubscriptionToken = publisher.Subscribe(this);
        //}

        //public virtual void Subscribe(TickDataStreamer publisher)
        //{
        //    UnsubscriptionToken = publisher.Subscribe(this);
        //}


        public int AlgoInstance
        {
            get
            { return _algoInstance; }
        }
    }
}
