using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Algorithms.Utilities;
using Global;
using KiteConnect;
using ZConnectWrapper;
//using Pub_Sub;
//using MarketDataTest;
using System.Data;

namespace Algos.TLogics
{
    public class ManageBox : IObserver<Tick[]>
    {
        static List<Instrument> BoxOptions = new List<Instrument>();

        //change it to activestrangles type data and store box id together..so that it can be retrieved and also it can be multiple boxes can run at the same time.
        static Dictionary<Int32, OptionsBox> ActiveBoxes = new Dictionary<int, OptionsBox>();
        //static int BoxID=0;
        static IEnumerable<decimal> strikePrices;
        static decimal currentBoxPremium;
        static decimal bInstrumentPrice = 0;
        const uint BASE_INSTRUMENT_TOKEN = 260105;
        const int BOX_SPREAD = 500;
        static DateTime OPTION_EXPIRY = Convert.ToDateTime("2019-09-13");
        static bool OrderPlaced;
        static int BoxCount = 0;

        public virtual void OnNext(Tick[] ticks)
        {
            lock (BoxOptions)
            {
                Tick baseInstrumentTick = ticks.FirstOrDefault(x => x.InstrumentToken == BASE_INSTRUMENT_TOKEN);
                if (baseInstrumentTick.LastPrice != 0)
                {
                    bInstrumentPrice = baseInstrumentTick.LastPrice;
                }
                else if (bInstrumentPrice == 0)
                {
                    return;
                }
                LoadBoxOptions(bInstrumentPrice, ticks);

                if (BoxCount < 1)
                {
                    BoxSpreadDynamic(ticks);
                }
                if (BoxCount > 0)
                {
             //       MonitorBox(ticks);
                }
            }
        }
       
        /// <summary>
        /// Choose Instruments, Get current Option Chain. Run Comparision Login. Place Limit Orders.
        /// Check the order status. Cancel order if full box is not executed. 
        /// Close the instrument when value is good. 
        /// </summary>
        private static void BoxSpreadDynamic(Tick[] ticks)
        {
            Instrument firstCallOption = new Instrument(), secondCallOption = new Instrument(), 
                firstPutOption = new Instrument(), secondPutOption = new Instrument();

            decimal boxValue = 0;
            decimal tempBoxValue = 0;



            foreach (decimal vstrike in strikePrices)
            {
                IEnumerable<decimal> hstrikePrices = strikePrices.Where(x => x < vstrike);

                foreach (decimal hstrike in hstrikePrices)
                {
                    //if (hstrike < vstrike)
                    //{
                    Dictionary<UInt32, Instrument> cpOption = BoxOptions.Where(x => x.Strike == hstrike).ToDictionary(i => i.InstrumentToken, i => i);
                    Instrument callOption1 = cpOption.First(x => x.Value.InstrumentType == "CE").Value;

                    if (callOption1.Offers == null || callOption1.Bids == null)
                        continue;

                    Instrument putOption1 = cpOption.First(x => x.Value.InstrumentType == "PE").Value;

                    if (putOption1.Offers == null || putOption1.Bids == null)
                        continue;

                    cpOption = BoxOptions.Where(x => x.Strike == vstrike).ToDictionary(i => i.InstrumentToken, i => i);

                    Instrument callOption2 = cpOption.First(x => x.Value.InstrumentType == "CE").Value;

                    if (callOption2.Offers == null || callOption2.Bids == null)
                        continue;

                    Instrument putOption2 = cpOption.First(x => x.Value.InstrumentType == "PE").Value;

                    if (putOption2.Offers == null || putOption2.Bids == null)
                        continue;


                    decimal buyOption = (vstrike - hstrike) - (callOption1.Offers[1].Price - putOption1.Bids[1].Price - callOption2.Bids[1].Price + putOption2.Offers[1].Price);
                    decimal sellOption = -(vstrike - hstrike) + (callOption1.Bids[1].Price - putOption1.Offers[1].Price - callOption2.Offers[1].Price + putOption2.Bids[1].Price);

                    tempBoxValue = Math.Max(buyOption, sellOption);

                    if (tempBoxValue > 0 && tempBoxValue > boxValue)
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
                    //}
                }
            }
            decimal[] avgPrice = new decimal[4];
            Dictionary<string, dynamic>[] orderStatus = new Dictionary<string, dynamic>[4];
            string[] orderIds = new string[4];
            //Place Orders if good price
            Logger.LogWrite(boxValue.ToString());
            Console.WriteLine(boxValue);
            if (boxValue > 50m /*&& !OrderPlaced*/ && firstCallOption.Offers[1].Price * firstPutOption.Bids[1].Price * secondPutOption.Bids[1].Price * secondCallOption.Offers[1].Price != 0 )
            {
                

                //OrderPlaced = true;
                BoxCount++;

                orderStatus[0] = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, firstCallOption.TradingSymbol.TrimEnd(),
                                      Constants.TRANSACTION_TYPE_BUY, 40, Product: Constants.PRODUCT_MIS,
                                      OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);
                orderStatus[1] = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, secondCallOption.TradingSymbol.TrimEnd(),
                                      Constants.TRANSACTION_TYPE_SELL, 40, Product: Constants.PRODUCT_MIS,
                                      OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);
                orderStatus[2] = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, firstPutOption.TradingSymbol.TrimEnd(),
                                      Constants.TRANSACTION_TYPE_SELL, 40, Product: Constants.PRODUCT_MIS,
                                      OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);
                orderStatus[3] = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, secondPutOption.TradingSymbol.TrimEnd(),
                                      Constants.TRANSACTION_TYPE_BUY, 40, Product: Constants.PRODUCT_MIS,
                                      OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);

                for (int i = 0; i < 4; i++)
                {
                    orderIds[i] = "0";
                    //decimal averagePrice = 0;
                    if (orderStatus[i]["data"]["order_id"] != null)
                    {
                        orderIds[i] = orderStatus[i]["data"]["order_id"];
                    }
                    if (orderIds[i] != "0")
                    {
                        System.Threading.Thread.Sleep(100); ///no needed in fast execution
                        List<Order> orderInfo = ZObjects.kite.GetOrderHistory(orderIds[i]);
                        avgPrice[i] = orderInfo[orderInfo.Count - 1].AveragePrice;
                    }
                    //if (avgPrice[i] == 0)
                    //    avgPrice = buyOrder ? currentPrice + 0.5m : currentPrice - 0.5m;
                    //return buyOrder ? averagePrice * -1 : averagePrice;
                }

                ///
                //avgPrice[0] = PlaceOrder(firstCallOption.TradingSymbol, true, firstCallOption.Offers[1].Price);
                //avgPrice[1] = PlaceOrder(secondCallOption.TradingSymbol, false, secondCallOption.Bids[1].Price);
                //avgPrice[2] = PlaceOrder(firstPutOption.TradingSymbol, false, firstPutOption.Bids[1].Price);
                //avgPrice[3] = PlaceOrder(secondPutOption.TradingSymbol, true, secondPutOption.Offers[1].Price);

                currentBoxPremium = Math.Abs(-avgPrice[0] + avgPrice[1] + avgPrice[2] - avgPrice[3]);


                Logger.LogWrite("Box Value Anticipated :" + boxValue.ToString());
                boxValue = Math.Abs(firstCallOption.Strike - secondCallOption.Strike) - currentBoxPremium;
                Logger.LogWrite("Box value locked:" + boxValue.ToString());
               


                DataLogic dl = new DataLogic();
                int boxID = dl.StoreBoxData(0, firstCallOption.InstrumentToken, secondCallOption.InstrumentToken,
                    firstPutOption.InstrumentToken, secondPutOption.InstrumentToken, avgPrice[0], avgPrice[1], avgPrice[2], avgPrice[3], boxValue);

                OptionsBox currentBox = new OptionsBox();

                Instrument[] instruments = new Instrument[4];
                instruments[0] = firstCallOption;
                instruments[1] = firstPutOption;
                instruments[2] = secondCallOption;
                instruments[3] = secondPutOption;

                currentBox.Instruments = instruments;
                currentBox.BoxValue = currentBoxPremium;

                ActiveBoxes.Add(boxID, currentBox);
                
                boxValue = 0;
            }
        }

        private static void MonitorBox(Tick[] ticks)
        {
            for (int i = 0; i < ActiveBoxes.Values.Count; i++)
            {
                KeyValuePair<int, OptionsBox> currentBox = ActiveBoxes.ElementAt(i);

                OptionsBox optionsBox = currentBox.Value;
                int boxId = currentBox.Key;

                Instrument[] options = optionsBox.Instruments;

                Instrument firstCallOption = options[0];
                Instrument firstPutOption = options[1];
                Instrument secondCallOption = options[2];
                Instrument secondPutOption = options[3];

                currentBoxPremium = optionsBox.BoxValue;
                //currentBoxPremium = Math.Abs(-firstCallOption.Offers[1].Price + secondCallOption.Bids[1].Price + firstPutOption.Bids[1].Price - secondPutOption.Offers[1].Price);

                firstCallOption = UpdateOptionPrice(firstCallOption, ticks);
                firstPutOption = UpdateOptionPrice(firstPutOption, ticks);
                secondCallOption = UpdateOptionPrice(secondCallOption, ticks);
                secondPutOption = UpdateOptionPrice(secondPutOption, ticks);

                decimal premuimOppty = Math.Abs(firstCallOption.Bids[1].Price - firstPutOption.Offers[1].Price - secondCallOption.Offers[1].Price + secondPutOption.Bids[1].Price);

                Console.WriteLine(premuimOppty - currentBoxPremium);
                Logger.LogWrite((premuimOppty - currentBoxPremium).ToString());

                decimal[] avgPrice = new decimal[4];
                Dictionary<string, dynamic>[] orderStatus = new Dictionary<string, dynamic>[4];
                string[] orderIds = new string[4];

                if (premuimOppty > (currentBoxPremium + 7m) /*&& OrderPlaced*/ && firstCallOption.Bids[1].Price * firstPutOption.Offers[1].Price * secondCallOption.Offers[1].Price * secondPutOption.Bids[1].Price != 0)
                {
                    //OrderPlaced = false;
                    //BoxCount--;

                    orderStatus[0] = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, firstCallOption.TradingSymbol.TrimEnd(),
                                      Constants.TRANSACTION_TYPE_SELL, 40, Product: Constants.PRODUCT_MIS,
                                      OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);
                    orderStatus[1] = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, secondCallOption.TradingSymbol.TrimEnd(),
                                          Constants.TRANSACTION_TYPE_BUY, 40, Product: Constants.PRODUCT_MIS,
                                          OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);
                    orderStatus[2] = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, firstPutOption.TradingSymbol.TrimEnd(),
                                          Constants.TRANSACTION_TYPE_BUY, 40, Product: Constants.PRODUCT_MIS,
                                          OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);
                    orderStatus[3] = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, secondPutOption.TradingSymbol.TrimEnd(),
                                          Constants.TRANSACTION_TYPE_SELL, 40, Product: Constants.PRODUCT_MIS,
                                          OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);

                    for (int j = 0; j < 4; j++)
                    {
                        orderIds[j] = "0";
                        //decimal averagePrice = 0;
                        if (orderStatus[j]["data"]["order_id"] != null)
                        {
                            orderIds[j] = orderStatus[j]["data"]["order_id"];
                        }
                        if (orderIds[j] != "0")
                        {
                            System.Threading.Thread.Sleep(100); ///no needed in fast execution
                            List<Order> orderInfo = ZObjects.kite.GetOrderHistory(orderIds[j]);
                            avgPrice[j] = orderInfo[orderInfo.Count - 1].AveragePrice;
                        }
                        //if (avgPrice[j] == 0)
                        //    avgPrice = buyOrder ? currentPrice + 0.5m : currentPrice - 0.5m;
                        //return buyOrder ? averagePrice * -1 : averagePrice;
                    }

                    //avgPrice[0] = PlaceOrder(firstCallOption.TradingSymbol, false, firstCallOption.Bids[1].Price);
                    //avgPrice[1] = PlaceOrder(secondCallOption.TradingSymbol, true, secondCallOption.Offers[1].Price);
                    //avgPrice[2] = PlaceOrder(secondPutOption.TradingSymbol, false, secondPutOption.Bids[1].Price);
                    //avgPrice[3] = PlaceOrder(firstPutOption.TradingSymbol, true, firstPutOption.Offers[1].Price);

                    Logger.LogWrite("Closing Box value Anticipated:" + (premuimOppty - currentBoxPremium).ToString());
                    decimal revisedBoxValue = Math.Abs(firstCallOption.Strike - secondCallOption.Strike) - Math.Abs(avgPrice[0] - avgPrice[1] - avgPrice[2] + avgPrice[3]);
                    Logger.LogWrite("Box value locked:" + revisedBoxValue.ToString());

                    DataLogic dl = new DataLogic();
                    dl.StoreBoxData(boxId, firstCallOption.InstrumentToken, secondCallOption.InstrumentToken,
                        firstPutOption.InstrumentToken, secondPutOption.InstrumentToken, avgPrice[0], -avgPrice[1], -avgPrice[2], avgPrice[3], revisedBoxValue);

                    ActiveBoxes.Remove(boxId);
                    currentBoxPremium = 0;
                }
            }
        }

        private static decimal PlaceOrder(string tradingSymbol, bool buyOrder, decimal currentPrice)
        {
            //place all 4 orders first and then calculate average price..order plancement should not delaY IN arbitrage case
            Dictionary<string, dynamic> orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, tradingSymbol.TrimEnd(),
                                      buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL, 80, Product: Constants.PRODUCT_MIS,
                                      OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);

            string orderId = "0";
            decimal averagePrice = 0;
            if (orderStatus["data"]["order_id"] != null)
            {
                orderId = orderStatus["data"]["order_id"];
            }
            if (orderId != "0")
            {
                //System.Threading.Thread.Sleep(200); ///no needed in fast execution
                List<Order> orderInfo = ZObjects.kite.GetOrderHistory(orderId);
                averagePrice = orderInfo[orderInfo.Count - 1].AveragePrice;
            }
            if (averagePrice == 0)
                averagePrice = buyOrder ? currentPrice + 0.5m : currentPrice - 0.5m;
            return buyOrder ? averagePrice * -1 : averagePrice;
        }
        public static void LoadBoxOptions(decimal _bInstrumentPrice, Tick[] ticks)
        {
            if (BoxOptions == null || BoxOptions.Where(x => Math.Abs(x.Strike - _bInstrumentPrice) <= BOX_SPREAD && x.Strike % 100 == 0).Select(x => x.Strike).Distinct().Count() < 2 * 2 * BOX_SPREAD/100)
            {
                DataLogic dl = new DataLogic();
                BoxOptions = dl.RetrieveOptions(BASE_INSTRUMENT_TOKEN, OPTION_EXPIRY);

                BoxOptions = BoxOptions.Where(x => Math.Abs(x.Strike - _bInstrumentPrice) <= BOX_SPREAD && x.Strike % 100 == 0).ToList();
                strikePrices = BoxOptions.Select(x => x.Strike).Distinct();
            }
            for(int i=0;i<BoxOptions.Count;i++)
            {
                BoxOptions[i] = UpdateOptionPrice(BoxOptions[i], ticks);
            }
        }

        private static Instrument UpdateOptionPrice(Instrument option, Tick[] ticks)
        {
            Tick optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == option.InstrumentToken);
            if (optionTick.LastPrice != 0)
            {
                option.LastPrice = optionTick.LastPrice;
                option.Bids = optionTick.Bids;
                option.Offers = optionTick.Offers;
            }
            return option;
        }

    public IDisposable UnsubscriptionToken;
        public virtual void Subscribe(Publisher publisher)
        {
            UnsubscriptionToken = publisher.Subscribe(this);
        }
        //public virtual void Subscribe(Ticker publisher)
        //{
        //    UnsubscriptionToken = publisher.Subscribe(this);
        //}

        //public virtual void Subscribe(TickDataStreamer publisher)
        //{
        //    UnsubscriptionToken = publisher.Subscribe(this);
        //}

        public virtual void Unsubscribe()
        {
            UnsubscriptionToken.Dispose();
        }

        public virtual void OnCompleted()
        {
        }
        public virtual void OnError(Exception ex)
        {
            ///TODO: Log the error. Also handle the error.
        }

    }
}
