using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KiteConnect;
using Global;
using AdvanceAlgos.Utilities;

using ZConnectWrapper;

namespace AdvanceAlgos.Algorithms
{
    class BOX
    {
        static Dictionary<UInt32, Instrument> BoxOptions;
        
        static Dictionary<UInt32, Instrument> CurrentBox;
        //static Dictionary<UInt32, Trade> TradedBox;
        static Dictionary<string, BoxStatus> OrderedBox;

        static IEnumerable<decimal> strikePrices;

        //static Dictionary<string, Dictionary<string, dynamic>> OrderStatus;
        
        
        static decimal currentBoxValue;
        
        static decimal currentPrice ;
        static bool OrderPlaced;
        static bool OrderExecuted;
        static bool MoreOrders= true;

        public static void StartBox()
        {
            BoxOptions = zConstants.GetNiftyInstruments(zConstants.options);

            SelectInstrumentDataForSubscription();// GetInstrumentWithLatestPrice();

            //BoxSpread();

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

            //KeyValuePair<UInt32, Instrument> firstCallOption = new KeyValuePair<UInt32, Instrument>();
            //KeyValuePair<UInt32, Instrument> secondCallOption = new KeyValuePair<UInt32, Instrument>();
            //KeyValuePair<UInt32, Instrument> firstPutOption = new KeyValuePair<UInt32, Instrument>();
            //KeyValuePair<UInt32, Instrument> secondPutOption = new KeyValuePair<UInt32, Instrument>();


            Instrument firstCallOption = new Instrument();
            Instrument secondCallOption = new Instrument();
            Instrument firstPutOption = new Instrument();
            Instrument secondPutOption = new Instrument();


            decimal boxValue = 0;
            decimal tempBoxValue = 0;

           

            foreach (decimal vstrike in strikePrices)
            {
                IEnumerable<decimal> hstrikePrices = strikePrices.Where(x => x < vstrike);

                foreach (decimal hstrike in strikePrices)
                {
                    if (hstrike < vstrike)
                    {
                        Dictionary<UInt32, Instrument> cpOption = BoxOptions.Where(x => x.Value.Strike == hstrike).ToDictionary(i => i.Key, i => i.Value);

                        Instrument callOption1 = cpOption.First(x => x.Value.InstrumentType == "CE").Value;
                        Instrument putOption1 = cpOption.First(x => x.Value.InstrumentType == "PE").Value;

                        cpOption = BoxOptions.Where(x => x.Value.Strike == vstrike).ToDictionary(i => i.Key, i => i.Value);

                        Instrument callOption2 = cpOption.First(x => x.Value.InstrumentType == "CE").Value;
                        Instrument putOption2 = cpOption.First(x => x.Value.InstrumentType == "PE").Value;

                        decimal buyOption = (vstrike - hstrike) - (callOption1.Offers[1].Price - putOption1.Bids[1].Price - callOption2.Bids[1].Price + putOption2.Offers[1].Price);
                        decimal sellOption = -(vstrike - hstrike) + (callOption1.Bids[1].Price - putOption1.Offers[1].Price - callOption2.Offers[1].Price + putOption2.Bids[1].Price);

                        tempBoxValue = Math.Max(buyOption,sellOption);

                       
                        if (tempBoxValue > boxValue)
                        {
                            boxValue = tempBoxValue;
                            if (buyOption > sellOption)
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




                        //Logger.LogWrite(string.Format("B:{0} | S:{1}",buyOption, sellOption));

                        //tempBoxValue = (vstrike - hstrike) - (callOption1.LastPrice - putOption1.LastPrice - callOption2.LastPrice + putOption2.LastPrice);
                        //tempBoxValue = (vstrike - hstrike) - //(callOption1.Offers[1].Price - putOption1.Bids[1].Price - callOption2.Bids[1].Price + putOption2.Offers[1].Price);
                        
                        //if (Math.Abs(tempBoxValue) > boxValue)
                        //{
                        //    boxValue = Math.Abs(tempBoxValue);

                        //    if (Math.Abs(buyOption) < Math.Abs(sellOption))
                        //    {
                        //        firstCallOption = callOption1;
                        //        secondCallOption = callOption2;
                        //        firstPutOption = putOption1;
                        //        secondPutOption = putOption2;
                        //    }
                        //    else
                        //    {
                        //        firstCallOption = callOption2;
                        //        secondCallOption = callOption1;
                        //        firstPutOption = putOption2;
                        //        secondPutOption = putOption1;
                        //    }
                        //}
                    }
                }
            }
            string[] orderIDs = new string[4];
            decimal[] avgPrice = new decimal[4];
            List<Order>[] orderInfos = new List<Order>[4];
            int count = 0;
            Dictionary<string, dynamic>[] orderStatus = new Dictionary<string, dynamic>[4];
            //Place Orders if good price
            if (boxValue > 5m && !OrderPlaced)// && firstCallOption.Offers[1].Price * firstPutOption.Bids[1].Price * secondPutOption.Bids[1].Price * secondCallOption.Offers[1].Price != 0)
            {
                OrderPlaced = true;
                    /* ///// GET QUOTE HERE BEFORE PLACING THE ORDER SO THAT YOU CAN SEE THAT THESE ORDERS ARE NOT AT VERY WRONG PRICE.PUT A BRACKET OF 5.
                     orderStatus[0] = Global.kite.PlaceOrder(Constants.EXCHANGE_NFO, firstCallOption.TradingSymbol, Constants.TRANSACTION_TYPE_BUY, 80, firstCallOption.Offers[1].Price + 0.1m, Constants.PRODUCT_MIS, Constants.ORDER_TYPE_MARKET, Constants.VALIDITY_DAY, 1, null, null, null, null, Constants.VARIETY_REGULAR, "");
                     orderStatus[1] = Global.kite.PlaceOrder(Constants.EXCHANGE_NFO, secondCallOption.TradingSymbol, Constants.TRANSACTION_TYPE_SELL, 80, secondCallOption.Bids[1].Price - 0.1m, Constants.PRODUCT_MIS, Constants.ORDER_TYPE_MARKET, Constants.VALIDITY_DAY, 1, null, null, null, null, Constants.VARIETY_REGULAR, "");
                     orderStatus[2] = Global.kite.PlaceOrder(Constants.EXCHANGE_NFO, secondPutOption.TradingSymbol, Constants.TRANSACTION_TYPE_BUY, 80, secondPutOption.Offers[1].Price + 0.1m, Constants.PRODUCT_MIS, Constants.ORDER_TYPE_MARKET, Constants.VALIDITY_DAY, 1, null, null, null, null, Constants.VARIETY_REGULAR, "");
                     orderStatus[3] = Global.kite.PlaceOrder(Constants.EXCHANGE_NFO, firstPutOption.TradingSymbol, Constants.TRANSACTION_TYPE_SELL, 80, firstPutOption.Bids[1].Price - 0.1m, Constants.PRODUCT_MIS, Constants.ORDER_TYPE_MARKET, Constants.VALIDITY_DAY, 1, null, null, null, null, Constants.VARIETY_REGULAR, "");

                     for (int i = 0; i < 4; i++)
                     {
                         orderIDs[i] = orderStatus[i]["data"]["order_id"];
                         orderInfos[i] = Global.kite.GetOrderHistory(orderIDs[i]);
                         count = orderInfos[i].Count;
                         avgPrice[i] = orderInfos[i][count-1].AveragePrice;
                     }
                */

                orderIDs[0] = "1234";
                orderIDs[1] = "2345";
                orderIDs[2] = "3456";
                orderIDs[3] = "4567";

                avgPrice[0] = firstCallOption.Offers[1].Price;
                avgPrice[1] = secondCallOption.Bids[1].Price;
                avgPrice[2] = secondPutOption.Offers[1].Price;
                avgPrice[3] = firstPutOption.Bids[1].Price;

                OrderedBox = new Dictionary<string, BoxStatus>();
               //// Two keys: Status & Data, order_id
                OrderedBox.Add(orderIDs[0], new BoxStatus(firstCallOption.InstrumentToken, avgPrice[0], 80));
                OrderedBox.Add(orderIDs[1], new BoxStatus(secondCallOption.InstrumentToken, avgPrice[1], 80));
                OrderedBox.Add(orderIDs[2], new BoxStatus(secondPutOption.InstrumentToken, avgPrice[2], 80));
                OrderedBox.Add(orderIDs[3], new BoxStatus(firstPutOption.InstrumentToken, avgPrice[3], 80));

              
                //Could be a sumple 4 element array of Instrument.Will be falster
                //ONLY AFTER SUCCESSFUL EXECUTION OF THE BOX STRATEGY
                Dictionary<UInt32, Instrument> boxInstruments = new Dictionary<UInt32, Instrument>();
                boxInstruments.Add(firstCallOption.InstrumentToken, firstCallOption);
                boxInstruments.Add(firstPutOption.InstrumentToken, firstPutOption);
                boxInstruments.Add(secondCallOption.InstrumentToken, secondCallOption);
                boxInstruments.Add(secondPutOption.InstrumentToken, secondPutOption);

                ///TODO: Premium 2 is added as buffer. This should come from GetLTP based on OrderID
                //premiumPaid = -firstCallOption.Offers[2].Price + firstPutOption.Bids[2].Price - secondPutOption.Offers[2].Price + secondCallOption.Bids[2].Price;
                premiumPaid = -avgPrice[0] + avgPrice[3] - avgPrice[2] + avgPrice[1];
                if (premiumPaid == 0)
                {
                    premiumPaid = -firstCallOption.Offers[1].Price + firstPutOption.Bids[1].Price - secondPutOption.Offers[1].Price + secondCallOption.Bids[1].Price;
                }
               

               

                //MADE TRUE AS ON ORDER UPDATE IS NOT WORKING
                OrderExecuted = true;
                CurrentBox = boxInstruments;
                currentBoxValue = -premiumPaid;

                //Update Ticker to monitor only Current Box
                Global.ticker.Subscribe(Tokens: CurrentBox.Keys.ToArray()); //.Select(x => x.InstrumentToken).ToArray());
                Global.ticker.SetMode(Tokens: CurrentBox.Keys.ToArray(), Mode: Constants.MODE_FULL);


                Global.UpdateActivity("Box Found", string.Format("{0} - {1}", firstPutOption.Strike, secondPutOption.Strike));
                Logger.LogWrite(String.Format("BOX Placed:{0} : {1} & {2}", boxValue, firstCallOption.Strike, secondCallOption.Strike));
            }
            else
            {
                OrderPlaced = false ;

                //MADE TRUE AS ON ORDER UPDATE IS NOT WORKING
                OrderExecuted = false;
                CurrentBox = null;
                currentBoxValue = 0;
                Logger.LogWrite(string.Format("Low opportunity: {0}", boxValue));
            }
        }


        private static void SelectInstrumentDataForSubscription()
        {
            //Below is all nifty options. Change it to any options
            Dictionary<UInt32, string> shareOptions = GenerateOptionSymbols(BoxOptions);


            string[] share = zConstants.optionBase.Split(',');
            UInt32 shareToken = UInt32.Parse(share[0]);
            string shareSymbol = string.Format("NSE:{0}", share[2]);

            shareOptions.Add(shareToken, shareSymbol);

            Dictionary<string, LTP> OptionsLTP = z.GetLTP(InstrumentId: shareOptions.Values.ToArray());

            Instrument boxOption;

             currentPrice = OptionsLTP[shareSymbol].LastPrice;

            foreach (KeyValuePair<string, LTP> optionLTP in OptionsLTP)
            {
                if (optionLTP.Key == shareSymbol)
                {
                    currentPrice = optionLTP.Value.LastPrice;
                }
                else if (BoxOptions.TryGetValue(optionLTP.Value.InstrumentToken, out boxOption))
                {
                    //Filter options set to only the interested ones
                    if (Math.Abs(currentPrice - boxOption.Strike) > 200 || boxOption.Strike % 100 != 0)
                    {
                        BoxOptions.Remove(optionLTP.Value.InstrumentToken);
                    }
                    else
                    {
                        boxOption.LastPrice = optionLTP.Value.LastPrice;
                        BoxOptions[optionLTP.Value.InstrumentToken] = boxOption;
                    }
                }
            }

            ///TODO:The BOXOption can become smaller to only focus area of 300-500. That way the ticker will monitor only those.  Subscribe only those smaller boxes
            ///TODO:DO THIS FILTER BEFORE TICKER STARTS CONTINOUS LOOP
         //   BoxOptions = BoxOptions.Where(x => Math.Abs(currentPrice - x.Value.Strike) <= 300 && x.Value.Strike % 100 == 0).ToDictionary(x=>x.Key, y=>y.Value);

            ///TODO:DO THIS FILTER BEFORE TICKER STARTS CONTINOUS LOOP
            // IEnumerable<decimal> strikePrices = BoxOptions.Where(x =>  Math.Abs(currentPrice - x.Value.Strike) <= 300 && x.Value.Strike%100==0).Select(x => x.Value.Strike).Distinct();
            strikePrices = BoxOptions.Select(x => x.Value.Strike).Distinct();
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
            Global.ticker.SetMode(Tokens: BoxOptions.Keys.ToArray(), Mode: Constants.MODE_FULL);
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
        private static void OnTick(Tick[] Ticks, Object state)
        {
            Instrument boxOption, instrumentLTP;


            //OrderPlaced = true;
            //OrderExecuted = true;

            

            foreach (Tick Tickdata in Ticks)
            {
                if (BoxOptions.TryGetValue(Tickdata.InstrumentToken, out boxOption))
                {
                    boxOption.LastPrice = Tickdata.LastPrice;
                    boxOption.Bids = Tickdata.Bids;
                    boxOption.Offers = Tickdata.Offers;
                    BoxOptions[Tickdata.InstrumentToken] = boxOption;
                }
                if (CurrentBox != null && CurrentBox.TryGetValue(Tickdata.InstrumentToken, out instrumentLTP))
                {
                    instrumentLTP.LastPrice = Tickdata.LastPrice;
                    instrumentLTP.Bids = Tickdata.Bids;
                    instrumentLTP.Offers = Tickdata.Offers;
                    CurrentBox[Tickdata.InstrumentToken] = instrumentLTP;
                }
            }
            if (OrderPlaced)
            {
                if (OrderExecuted)
                {
                    Global.CurrentActivity = "Looking to Close";
                    Global.UpdateActivity("Looking to Close");

                    //SetOrderedBox(25500m, 25800m, 281.55m);
                    //MonitorBox(CurrentBox);

                    //SetOrderedBox(25300m, 25600m, 280.45m);
                    MonitorBox(CurrentBox);

                }
            }
            else
            {
                Global.CurrentActivity = "Searching for Box";
                Global.UpdateActivity("Searching for Box", "-");
                BoxSpread();
            }

          
        }
        private static void SetOrderedBox(decimal strikePrice1, decimal strikePrice2, decimal boxValue)
        {

            Dictionary<UInt32, Instrument> cpOption = BoxOptions.Where(x => x.Value.Strike == strikePrice1).ToDictionary(i => i.Key, i => i.Value);

            Instrument callOption1 = cpOption.First(x => x.Value.InstrumentType == "CE").Value;
            Instrument putOption1 = cpOption.First(x => x.Value.InstrumentType == "PE").Value;

            cpOption = BoxOptions.Where(x => x.Value.Strike == strikePrice2).ToDictionary(i => i.Key, i => i.Value);

            Instrument callOption2 = cpOption.First(x => x.Value.InstrumentType == "CE").Value;
            Instrument putOption2 = cpOption.First(x => x.Value.InstrumentType == "PE").Value;


            Instrument firstCallOption = new Instrument();
            Instrument secondCallOption = new Instrument();
            Instrument firstPutOption = new Instrument();
            Instrument secondPutOption = new Instrument();


            firstCallOption = callOption1;
            secondCallOption = callOption2;
            firstPutOption = putOption1;
            secondPutOption = putOption2;



            Dictionary<UInt32, Instrument> boxInstruments = new Dictionary<UInt32, Instrument>();
            boxInstruments.Add(firstCallOption.InstrumentToken, firstCallOption);
            boxInstruments.Add(firstPutOption.InstrumentToken, firstPutOption);
            boxInstruments.Add(secondCallOption.InstrumentToken, secondCallOption);
            boxInstruments.Add(secondPutOption.InstrumentToken, secondPutOption);

            CurrentBox = boxInstruments;

            ///TODO: Premium 2 is added as buffer. This should come from GetLTP based on OrderID
            //premiumPaid = -firstCallOption.Offers[2].Price + firstPutOption.Bids[2].Price - secondPutOption.Offers[2].Price + secondCallOption.Bids[2].Price;
            currentBoxValue = boxValue;



        }

        private static void MonitorBox(Dictionary<UInt32, Instrument> CurrentBox)
        {
            //Instrument instrumentLTP;
            //if (CurrentBox.TryGetValue(TickData.InstrumentToken, out instrumentLTP))
            //{
            //    instrumentLTP.LastPrice = TickData.LastPrice;
            //    CurrentBox[TickData.InstrumentToken] = instrumentLTP;
            //}
            //else
            //{
            //    return;
            //}

            Instrument firstCallOption = CurrentBox.ElementAt(0).Value;
            Instrument firstPutOption = CurrentBox.ElementAt(1).Value;
            Instrument secondCallOption = CurrentBox.ElementAt(2).Value;
            Instrument secondPutOption = CurrentBox.ElementAt(3).Value;

            //decimal premuimOppty = firstCallOption.LastPrice - firstPutOption.LastPrice - secondCallOption.LastPrice + secondPutOption.LastPrice;
            decimal premuimOppty = firstCallOption.Bids[1].Price - firstPutOption.Offers[1].Price - secondCallOption.Offers[1].Price + secondPutOption.Bids[1].Price;

            if (premuimOppty > (currentBoxValue + 3m) && MoreOrders)
            {

                //ONLY AFTER SUCCESSFUL EXECUTION OF THE BOX STRATEGY
                OrderPlaced = false;
                MoreOrders = false;
                /*

                Dictionary<string, dynamic>[] orderStatus = new Dictionary<string, dynamic>[4];
                string[] orderIDs = new string[4];
                decimal[] avgPrice = new decimal[4];
                List<Order>[] orderInfos = new List<Order>[4];
                int count = 0;

                ///// GET QUOTE HERE BEFORE PLACING THE ORDER SO THAT YOU CAN SEE THAT THESE ORDERS ARE NOT AT VERY WRONG PRICE.PUT A BRACKET OF 5.
                orderStatus[0] = Global.kite.PlaceOrder(Constants.EXCHANGE_NFO, firstCallOption.TradingSymbol, Constants.TRANSACTION_TYPE_SELL, 80, firstCallOption.Bids[1].Price - 0.1m, Constants.PRODUCT_MIS, Constants.ORDER_TYPE_MARKET, Constants.VALIDITY_DAY, 1, null, null, null, null, Constants.VARIETY_REGULAR, "");
                orderStatus[1] = Global.kite.PlaceOrder(Constants.EXCHANGE_NFO, secondCallOption.TradingSymbol, Constants.TRANSACTION_TYPE_BUY, 80, secondCallOption.Offers[1].Price + 0.1m, Constants.PRODUCT_MIS, Constants.ORDER_TYPE_MARKET, Constants.VALIDITY_DAY, 1, null, null, null, null, Constants.VARIETY_REGULAR, "");
                orderStatus[2] = Global.kite.PlaceOrder(Constants.EXCHANGE_NFO, secondPutOption.TradingSymbol, Constants.TRANSACTION_TYPE_SELL, 80, secondPutOption.Bids[1].Price - 0.1m, Constants.PRODUCT_MIS, Constants.ORDER_TYPE_MARKET, Constants.VALIDITY_DAY, 1, null, null, null, null, Constants.VARIETY_REGULAR, "");
                orderStatus[3] = Global.kite.PlaceOrder(Constants.EXCHANGE_NFO, firstPutOption.TradingSymbol, Constants.TRANSACTION_TYPE_BUY, 80, firstPutOption.Offers[1].Price + 0.1m, Constants.PRODUCT_MIS, Constants.ORDER_TYPE_MARKET, Constants.VALIDITY_DAY, 1, null, null, null, null, Constants.VARIETY_REGULAR, "");

                for (int i = 0; i < 4; i++)
                {
                    orderIDs[i] = orderStatus[i]["data"]["order_id"];
                    orderInfos[i] = Global.kite.GetOrderHistory(orderIDs[i]);
                    count = orderInfos[i].Count;
                    avgPrice[i] = orderInfos[i][count - 1].AveragePrice;
                }


                //// Two keys: Status & Data, order_id
                OrderedBox = new Dictionary<string, BoxStatus>();
                OrderedBox.Add(orderIDs[0], new BoxStatus(firstCallOption.InstrumentToken, avgPrice[0], 80));
                OrderedBox.Add(orderIDs[1], new BoxStatus(secondCallOption.InstrumentToken, avgPrice[1], 80));
                OrderedBox.Add(orderIDs[2], new BoxStatus(secondPutOption.InstrumentToken, avgPrice[2], 80));
                OrderedBox.Add(orderIDs[3], new BoxStatus(firstPutOption.InstrumentToken, avgPrice[3], 80));


                //// Two keys: Status & Data, order_id
                //OrderedBox = new Dictionary<string, BoxStatus>();
                //OrderedBox.Add(firstCallOrder["data"]["order_id"], new BoxStatus(firstCallOption.InstrumentToken, 0, 80));
                //OrderedBox.Add(secondCallOrder["data"]["order_id"], new BoxStatus(secondCallOption.InstrumentToken, 0, 80));
                //OrderedBox.Add(secondPutOrder["data"]["order_id"], new BoxStatus(secondPutOption.InstrumentToken, 0, 80));
                //OrderedBox.Add(firstPutOrder["data"]["order_id"], new BoxStatus(firstPutOption.InstrumentToken, 0, 80));



                ///TODO: Premium 2 is added as buffer. This should come from GetLTP based on OrderID
                //premiumPaid = -firstCallOption.Offers[2].Price + firstPutOption.Bids[2].Price - secondPutOption.Offers[2].Price + secondCallOption.Bids[2].Price;
                decimal premuimEarned = -avgPrice[0] + avgPrice[3] - avgPrice[2] + avgPrice[1];
                if (premuimEarned == 0)
                {
                    premuimEarned = premuimOppty;
                }

               


                Logger.LogWrite(String.Format("BOX:{0} : {1} & {2}", premuimEarned - currentBoxValue, firstCallOption.Strike, secondCallOption.Strike));

                Global.UpdatePandL(premuimEarned - currentBoxValue);
                */

                //ONLY AFTER SUCCESSFUL EXECUTION OF THE BOX STRATEGY
                //OrderPlaced = false;
                ///MAKING ORDEREXECUTED TRUE ON ORDER UPDATE IS NOT WORKING
                OrderedBox = null;
                OrderExecuted = true;
                CurrentBox = null;
                currentBoxValue = 0;

                //Update Ticker to monitor only Current Box
                Global.ticker.Subscribe(Tokens: BoxOptions.Keys.ToArray()); //.Select(x => x.InstrumentToken).ToArray());
                Global.ticker.SetMode(Tokens: BoxOptions.Keys.ToArray(), Mode: Constants.MODE_FULL);

                Global.UpdateActivity("Box Closed", "-");
                Global.StopTicker();
                //Environment.Exit(0);
                
            }
            else
            {
                Logger.LogWrite(String.Format("No Sale Oppty: {0}: {1},{2}-{3},{4}", premuimOppty - currentBoxValue, firstCallOption.LastPrice, firstPutOption.LastPrice, secondCallOption.LastPrice, secondPutOption.LastPrice));
            }
            
        }

       

        private static void OnOrderUpdate(Order OrderData)
        {
           // Logger.LogWrite("OrderUpdate " + Utils.JsonSerialize(OrderData));

            //UpdateTradedBox(OrderData);


            //if (IsBoxExecutionOver())
            //{
            //    OrderExecuted = true;

            //    currentBoxValue = OrderedBox.ElementAt(0).Value.AveragePrice - OrderedBox.ElementAt(1).Value.AveragePrice
            //        + OrderedBox.ElementAt(2).Value.AveragePrice - OrderedBox.ElementAt(3).Value.AveragePrice;

            //    OrderedBox = null;
            //    Logger.LogWrite(String.Format("BOX Executed"));//:{0} : {1} & {2}", boxValue, firstCallOption.Value.Strike, secondCallOption.Value.Strike));
            //}

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
            if (OrderedBox.TryGetValue(OrderData.OrderId, out executedOrder))
            {
                executedOrder.PendingQuantity = OrderData.PendingQuantity;
                executedOrder.AveragePrice = OrderData.Price;
                OrderedBox[OrderData.OrderId] = executedOrder;
            }
        }
        private static bool IsBoxExecutionOver()
        {
            bool orderPending = false;
            foreach(KeyValuePair<string, BoxStatus> order in OrderedBox)
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
