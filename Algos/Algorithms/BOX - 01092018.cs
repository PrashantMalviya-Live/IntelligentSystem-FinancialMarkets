using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KiteConnect;

using AdvanceAlgos.Utilities;



namespace AdvanceAlgos.Algorithms
{
    class BOX
    {
        static Dictionary<UInt32, Instrument> BoxOptions;
        static Dictionary<UInt32, Instrument> CurrentBox;
        static Dictionary<UInt32, Trade> TradedBox;
        static Dictionary<UInt32, BoxStatus> OrderedBox;

        static decimal currentBoxValue;
        static Instrument boxOption;
        static decimal currentPrice = 11400;
        static bool OrderPlaced;
        static bool OrderExecuted;

        public static void StartBox()
        {
            BoxOptions = zConstants.GetNiftyInstruments(zConstants.options);
            
            GetInstrumentWithLatestPrice();
            
            BoxSpread();

            // Initialize ticker
            initTicker();
        }


        /// <summary>
        /// Choose Instruments, Get current Option Chain. Run Comparision Login. Please Limit Orders.
        /// Check the order status. Cancel order if full box is not executed. 
        /// Close the instrument when value is good. 
        /// </summary>
        private static void BoxSpread()
        {
            decimal premiumPaid = 0;

            KeyValuePair<UInt32, Instrument> firstCallOption = new KeyValuePair<UInt32, Instrument>();
            KeyValuePair<UInt32, Instrument> secondCallOption = new KeyValuePair<UInt32, Instrument>();
            KeyValuePair<UInt32, Instrument> firstPutOption = new KeyValuePair<UInt32, Instrument>();
            KeyValuePair<UInt32, Instrument> secondPutOption = new KeyValuePair<UInt32, Instrument>();

            decimal boxValue = 0;
            decimal tempBoxValue = 0;

            ///TODO:The BOXOption can become smaller to only focus area of 300-500. That way the ticker will monitor only those.  Subscribe only those smaller boxes
            IEnumerable<decimal> strikePrices = BoxOptions.Where(x =>  Math.Abs(currentPrice - x.Value.Strike) <= 300 && x.Value.Strike%100==0).Select(x => x.Value.Strike).Distinct();

            foreach (decimal vstrike in strikePrices)
            {
                IEnumerable<decimal> hstrikePrices = strikePrices.Where(x => x < vstrike);

                foreach (decimal hstrike in strikePrices)
                {
                    if (hstrike < vstrike)
                    {
                        Dictionary<UInt32, Instrument> cpOption = BoxOptions.Where(x => x.Value.Strike == hstrike).ToDictionary(i => i.Key, i => i.Value);

                        KeyValuePair<UInt32,Instrument> callOption1 = cpOption.First(x => x.Value.InstrumentType == "CE");
                        KeyValuePair<UInt32, Instrument> putOption1 = cpOption.First(x => x.Value.InstrumentType == "PE");

                        cpOption = BoxOptions.Where(x => x.Value.Strike == vstrike).ToDictionary(i => i.Key, i => i.Value);

                        KeyValuePair<UInt32, Instrument> callOption2 = cpOption.First(x => x.Value.InstrumentType == "CE");
                        KeyValuePair<UInt32, Instrument> putOption2 = cpOption.First(x => x.Value.InstrumentType == "PE");

                        tempBoxValue = (vstrike - hstrike) - (callOption1.Value.LastPrice - putOption1.Value.LastPrice - callOption2.Value.LastPrice + putOption2.Value.LastPrice);
                        
                        if (Math.Abs(tempBoxValue) > boxValue)
                        {
                            boxValue = Math.Abs(tempBoxValue);

                            if (tempBoxValue > 0)
                            {
                                firstCallOption = callOption1;
                                secondCallOption = callOption2;
                                firstPutOption = putOption1;
                                secondPutOption = putOption2;
                            }
                            else
                            {
                                firstCallOption = callOption2;
                                secondCallOption = callOption1;
                                firstPutOption = putOption2;
                                secondPutOption = putOption1;
                            }
                        }
                    }
                }
            }
            //Place Orders if good price
            if (boxValue > 10 && firstCallOption.Value.LastPrice * firstPutOption.Value.LastPrice * secondPutOption.Value.LastPrice * secondCallOption.Value.LastPrice != 0)
            {

                ///// GET QUOTE HERE BEFORE PLACING THE ORDER SO THAT YOU CAN SEE THAT THESE ORDERS ARE NOT AT VERY WRONG PRICE.PUT A BRACKET OF 5.
                Dictionary<string, dynamic> firstCallOrder = Global.kite.PlaceOrder(Constants.EXCHANGE_NFO, firstCallOption.Value.TradingSymbol, Constants.TRANSACTION_TYPE_BUY, 75, firstCallOption.Value.LastPrice + 0.5m, Constants.PRODUCT_NRML, Constants.ORDER_TYPE_LIMIT, Constants.VALIDITY_DAY, 1, null, null, null, null, Constants.VARIETY_REGULAR, "");
                Dictionary<string, dynamic> secondCallOrder= Global.kite.PlaceOrder(Constants.EXCHANGE_NFO, secondCallOption.Value.TradingSymbol, Constants.TRANSACTION_TYPE_SELL, 75, secondCallOption.Value.LastPrice - 0.5m, Constants.PRODUCT_NRML, Constants.ORDER_TYPE_LIMIT, Constants.VALIDITY_DAY, 1, null, null, null, null, Constants.VARIETY_REGULAR, "");
                Dictionary<string, dynamic> secondPutOrder= Global.kite.PlaceOrder(Constants.EXCHANGE_NFO, secondPutOption.Value.TradingSymbol, Constants.TRANSACTION_TYPE_BUY, 75, secondPutOption.Value.LastPrice + 0.5m, Constants.PRODUCT_NRML, Constants.ORDER_TYPE_LIMIT, Constants.VALIDITY_DAY, 1, null, null, null, null, Constants.VARIETY_REGULAR, "");
                Dictionary<string, dynamic> firstPutOrder = Global.kite.PlaceOrder(Constants.EXCHANGE_NFO, firstPutOption.Value.TradingSymbol, Constants.TRANSACTION_TYPE_SELL, 75, firstPutOption.Value.LastPrice - 0.5m, Constants.PRODUCT_NRML, Constants.ORDER_TYPE_LIMIT, Constants.VALIDITY_DAY, 1, null, null, null, null, Constants.VARIETY_REGULAR, "");

                //BoxStatus boxStatus = new BoxStatus();
                //OrderId = data["order_id"];
                //AveragePrice = data["average_price"];
                //PendingQuantity = data["pending_quantity"];

               // Two keys: Status & Data, order_id
                OrderedBox.Add(firstCallOption.Value.InstrumentToken, new BoxStatus(new Dictionary<string, dynamic>() { {"order_id", firstCallOrder["data"]["order_id"]},{"average_price",0},{"pending_quantity",75}}));
                OrderedBox.Add(secondCallOption.Value.InstrumentToken, new BoxStatus(new Dictionary<string, dynamic>() { { "order_id", secondCallOrder["data"]["order_id"] }, { "average_price", 0 }, { "pending_quantity", 75 } }));
                OrderedBox.Add(secondPutOption.Value.InstrumentToken, new BoxStatus(new Dictionary<string, dynamic>() { { "order_id", secondPutOrder["data"]["order_id"] }, { "average_price", 0 }, { "pending_quantity", 75 } }));
                OrderedBox.Add(firstPutOption.Value.InstrumentToken, new BoxStatus(new Dictionary<string, dynamic>() { { "order_id", firstPutOrder["data"]["order_id"] }, { "average_price", 0 }, { "pending_quantity", 75 } }));

                //OrderedBox.Add(secondCallOption.Value.InstrumentToken, secondCallOrder);
                //OrderedBox.Add(secondPutOption.Value.InstrumentToken, secondPutOrder);
                //OrderedBox.Add(firstPutOption.Value.InstrumentToken, firstPutOrder);
                
                //ONLY AFTER SUCCESSFUL EXECUTION OF THE BOX STRATEGY
                Dictionary<UInt32, Instrument> boxInstruments = new Dictionary<UInt32, Instrument>();
                boxInstruments.Add(firstCallOption.Key, firstCallOption.Value);
                boxInstruments.Add(firstPutOption.Key, firstPutOption.Value);
                boxInstruments.Add(secondCallOption.Key, secondCallOption.Value);
                boxInstruments.Add(secondPutOption.Key, secondPutOption.Value);

                premiumPaid = -firstCallOption.Value.LastPrice + firstPutOption.Value.LastPrice - secondPutOption.Value.LastPrice + secondCallOption.Value.LastPrice;

                OrderPlaced = true;
                OrderExecuted = false;
                CurrentBox = boxInstruments;
                currentBoxValue = -premiumPaid;

                Logger.LogWrite(String.Format("BOX Placed:{0} : {1} & {2}", boxValue, firstCallOption.Value.Strike, secondCallOption.Value.Strike));
            }
            else
            {
                Logger.LogWrite(string.Format("Low opportunity: {0}", tempBoxValue));
            }
        }


        private static bool GetInstrumentWithLatestPrice()
        {
            //Below is all nifty options. Change it to any options
            Dictionary<UInt32, string> shareOptions = GenerateOptionSymbols(BoxOptions);


            string[] share = zConstants.optionBase.Split(',');
            UInt32 shareToken = UInt32.Parse(share[0]);
            string shareSymbol = string.Format("NSE:{0}", share[2]);

            shareOptions.Add(shareToken, shareSymbol);

            Dictionary<string, LTP> OptionsLTP = Global.kite.GetLTP(InstrumentId: shareOptions.Values.ToArray());

            foreach (KeyValuePair<string, LTP> optionLTP in OptionsLTP)
            {

                if (optionLTP.Key == shareSymbol)
                {
                    currentPrice = optionLTP.Value.LastPrice;
                }
                else if (BoxOptions.TryGetValue(optionLTP.Value.InstrumentToken, out boxOption))
                {
                    boxOption.LastPrice = optionLTP.Value.LastPrice;
                    BoxOptions[optionLTP.Value.InstrumentToken] = boxOption;
                }
            }

            return true;
        }

        private static string[] GenerateOptionSymbols(int incremental, string expiry, string baseInstrumentSymbol, int startPrice, int endPrice)
        {
            List<string> optionSymbols = new List<string>();

            System.Text.StringBuilder callOptionStringBuilder = new System.Text.StringBuilder();
            System.Text.StringBuilder putOptionStringBuilder = new System.Text.StringBuilder();


            for (int i = startPrice; i <= endPrice; i=i+incremental)
            {
                callOptionStringBuilder.Clear();
                putOptionStringBuilder.Clear();

                callOptionStringBuilder.AppendFormat("{0]{1}{2}CE", baseInstrumentSymbol, expiry, i);
                putOptionStringBuilder.AppendFormat("{0]{1}{2}PE", baseInstrumentSymbol, expiry, i);
                
                optionSymbols.Add(callOptionStringBuilder.ToString());
                optionSymbols.Add(putOptionStringBuilder.ToString());
            
            }
            return optionSymbols.ToArray();
        }
        private static Dictionary<UInt32, string> GenerateOptionSymbols(Dictionary<UInt32, Instrument> options)
        {
            Dictionary<UInt32, string> optionSymbols = new Dictionary<UInt32, string>();

            foreach (KeyValuePair<UInt32, Instrument> option in options)
            {
                optionSymbols.Add(option.Key, string.Format("NFO:{0}", option.Value.TradingSymbol));
            }

            return optionSymbols; ;
        }
        
        private static void initTicker()
        {
            Global.ticker = new Ticker(Login.MyAPIKey, Login.MyAccessToken, null);//State:zSessionState.Current);

            Global.ticker.OnTick += OnTick;
            Global.ticker.OnReconnect += OnReconnect;
            Global.ticker.OnNoReconnect += OnNoReconnect;
            Global.ticker.OnError += OnError;
            Global.ticker.OnClose += OnClose;
            Global.ticker.OnConnect += OnConnect;
            Global.ticker.OnOrderUpdate += OnOrderUpdate;

            Global.ticker.EnableReconnect(Interval: 5, Retries: 50);
            Global.ticker.Connect();

            // Subscribing to NIFTY50 and setting mode to LTP
            Global.ticker.Subscribe(Tokens: BoxOptions.Keys.ToArray()); //.Select(x => x.InstrumentToken).ToArray());
            Global.ticker.SetMode(Tokens: BoxOptions.Keys.ToArray(), Mode: Constants.MODE_LTP);
        }

     
        private static void OnConnect()
        {
            Logger.LogWrite("Connected ticker");
        }

        private static void OnClose()
        {
            Logger.LogWrite("Closed ticker");
        }

        private static void OnError(string Message)
        {
            Logger.LogWrite("Error: " + Message);
        }

        private static void OnNoReconnect()
        {
            Logger.LogWrite("Ticker not reconnecting");
        }

        private static void OnReconnect()
        {
            Logger.LogWrite("Reconnecting Ticker");
        }
        public static String GetTimestamp(DateTime value)
        {
            return value.ToString("yyyyMMddHHmmssffff");
        }
        private static void OnTick(Tick TickData, Object state)
        {
            if (BoxOptions.TryGetValue(TickData.InstrumentToken, out boxOption))
            {
                boxOption.LastPrice = TickData.LastPrice;
                BoxOptions[TickData.InstrumentToken] = boxOption;
            }

            if (OrderPlaced)
            {
                if (OrderExecuted)
                {
                    MonitorBox(TickData);
                }
            }
            else
            {
                BoxSpread();
            }
        }

        private static void MonitorBox(Tick TickData)
        {
            Instrument instrumentLTP;
            if (CurrentBox.TryGetValue(TickData.InstrumentToken, out instrumentLTP))
            {
                instrumentLTP.LastPrice = TickData.LastPrice;
                CurrentBox[TickData.InstrumentToken] = instrumentLTP;
            }
            else
            {
                return;
            }

            Instrument firstCallOption = CurrentBox.ElementAt(0).Value;
            Instrument firstPutOption = CurrentBox.ElementAt(1).Value;
            Instrument secondCallOption = CurrentBox.ElementAt(2).Value;
            Instrument secondPutOption = CurrentBox.ElementAt(3).Value;

            decimal premuimOppty = firstCallOption.LastPrice-firstPutOption.LastPrice-secondCallOption.LastPrice+secondPutOption.LastPrice;

            if (premuimOppty > currentBoxValue + 5)
            {
                ///// GET QUOTE HERE BEFORE PLACING THE ORDER SO THAT YOU CAN SEE THAT THESE ORDERS ARE NOT AT VERY WRONG PRICE.PUT A BRACKET OF 5.
                //kite.PlaceOrder(Constants.EXCHANGE_NFO, firstCallOption.TradingSymbol, Constants.TRANSACTION_TYPE_SELL, 75, firstCallOption.LastPrice - 0.5m, Constants.PRODUCT_NRML, Constants.ORDER_TYPE_LIMIT, Constants.VALIDITY_DAY, 1, null, null, null, null, Constants.VARIETY_REGULAR, "");
                //kite.PlaceOrder(Constants.EXCHANGE_NFO, secondCallOption.TradingSymbol, Constants.TRANSACTION_TYPE_BUY, 75, secondCallOption.LastPrice + 0.5m, Constants.PRODUCT_NRML, Constants.ORDER_TYPE_LIMIT, Constants.VALIDITY_DAY, 1, null, null, null, null, Constants.VARIETY_REGULAR, "");
                //kite.PlaceOrder(Constants.EXCHANGE_NFO, secondPutOption.TradingSymbol, Constants.TRANSACTION_TYPE_SELL, 75, secondPutOption.LastPrice - 0.5m, Constants.PRODUCT_NRML, Constants.ORDER_TYPE_LIMIT, Constants.VALIDITY_DAY, 1, null, null, null, null, Constants.VARIETY_REGULAR, "");
                //kite.PlaceOrder(Constants.EXCHANGE_NFO, firstPutOption.TradingSymbol, Constants.TRANSACTION_TYPE_BUY, 75, firstPutOption.LastPrice + 0.5m, Constants.PRODUCT_NRML, Constants.ORDER_TYPE_LIMIT, Constants.VALIDITY_DAY, 1, null, null, null, null, Constants.VARIETY_REGULAR, "");

                Logger.LogWrite(String.Format("BOX:{0} : {1} & {2}", premuimOppty - currentBoxValue, firstCallOption.Strike, secondCallOption.Strike));

                //ONLY AFTER SUCCESSFUL EXECUTION OF THE BOX STRATEGY
                OrderPlaced = false;
                CurrentBox = null;
                currentBoxValue = 0;
            }
            else
            {
                Logger.LogWrite(String.Format("No Sale Oppty: {0}: {1},{2}-{3},{4}", premuimOppty - currentBoxValue, firstCallOption.LastPrice, firstPutOption.LastPrice, secondCallOption.LastPrice, secondPutOption.LastPrice));
            }
            
        }

        private static void OnOrderUpdate(Order OrderData)
        {
           // Logger.LogWrite("OrderUpdate " + Utils.JsonSerialize(OrderData));

            UpdateTradedBox(OrderData);


            if(IsBoxExecutionOver())
            {
                OrderExecuted = true;

                OrderedBox = null;

                Logger.LogWrite(String.Format("BOX Executed"));//:{0} : {1} & {2}", boxValue, firstCallOption.Value.Strike, secondCallOption.Value.Strike));
            }

            //IF ALL FOUR ORDERS DID NOT GET EXECUTED THEN CANCEL THE ORDER
        }
        private static void PrepareForMonitoring()
        {
            //Dictionary<UInt32, Instrument> boxInstruments = new Dictionary<UInt32, Instrument>();
            //boxInstruments.Add(firstCallOption.Key, firstCallOption.Value);
            //boxInstruments.Add(firstPutOption.Key, firstPutOption.Value);
            //boxInstruments.Add(secondCallOption.Key, secondCallOption.Value);
            //boxInstruments.Add(secondPutOption.Key, secondPutOption.Value);

            //premiumPaid = -firstCallOption.Value.LastPrice + firstPutOption.Value.LastPrice - secondPutOption.Value.LastPrice + secondCallOption.Value.LastPrice;

            ////premiumPaid = -OrderedBox. + firstPutOption.Value.LastPrice - secondPutOption.Value.LastPrice + secondCallOption.Value.LastPrice;

            //OrderPlaced = true;
            //OrderExecuted = true;
            //CurrentBox = boxInstruments;
            //currentBoxValue = -premiumPaid;
        }
        private static void UpdateTradedBox(Order OrderData)
        {
            BoxStatus executedOrder;
            if (OrderedBox.TryGetValue(OrderData.InstrumentToken, out executedOrder))
            {
                executedOrder.PendingQuantity = OrderData.PendingQuantity;
                executedOrder.AveragePrice = OrderData.Price;
                OrderedBox[OrderData.InstrumentToken] = executedOrder;
            }
        }
        private static bool IsBoxExecutionOver()
        {
            bool orderPending = false;
            foreach(KeyValuePair<UInt32, BoxStatus> order in OrderedBox)
            {
                if(order.Value.PendingQuantity !=0)
                {
                    orderPending = true;
                    break;
                }
            }
            return orderPending;
        }
    }
}
