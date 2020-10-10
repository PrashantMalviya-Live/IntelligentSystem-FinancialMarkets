using Algorithms.Utilities;
using Algorithms.Utilities;
using GlobalLayer;
//using Microsoft.Office.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZMQFacade;
using DataAccess;

namespace Algos.TLogics
{
    public class OptionOptimizer : IZMQ
    {
        uint _instrumentToken = 0;
        DateTime _endDateTime;
        decimal _maxDrawDown = 0;
        DateTime[] _expiryDates;
        Option[][] suggestedOptions;
        //Tick[] _ticks;
        DateTime tradeTime;
        decimal baseInstrumentPrice = 0;
        SortedList<decimal, Dictionary<int, Option>[]> optimizedTrades = new SortedList<decimal, Dictionary<int, Option>[]>(new DuplicateKeyComparer<decimal>());
        Option[] calls;
        Option[] puts;
        public OptionOptimizer(DateTime endDateTime, uint instrumentToken, decimal maxDrawDown, DateTime[] expiryDates)
        {
            _endDateTime = endDateTime;
            _instrumentToken = instrumentToken;
            _maxDrawDown = maxDrawDown;
            _expiryDates = expiryDates;
        }
        public virtual async Task<bool> OnNext(Tick[] ticks)
        {
            //_instrumentToken = 256265;
            //var fema; var sema;
            //_ticks = ticks;
            
            List<Tick> validTicks = new List<Tick>();
            for (int i = 0; i < ticks.Count(); i++)
            {
                Tick tick = ticks[i];
                //if (ticks[i].InstrumentToken == _instrumentToken)
                //{
                //    validTicks.Add(tick);
                //}
                if (ticks[i].InstrumentToken == _instrumentToken)
                {
                    baseInstrumentPrice = tick.LastPrice;
                    tradeTime = tick.LastTradeTime.HasValue? tick.LastTradeTime.Value: tick.Timestamp.Value;
                }

            }
            if (baseInstrumentPrice == 0)
            {
                return true;
            }
            SuggestTrades(_maxDrawDown, _endDateTime, baseInstrumentPrice, _expiryDates);

            return true;
        }
        /// <summary>
        /// This process should be async and provide result when ready..and it should be called slow, may be at candle end
        /// </summary>
        /// <param name="maxDrawdown"></param>
        /// <param name="endDate"></param>
        /// <param name="atmStrike"></param>
        /// <param name="expiryDates"></param>
        public void SuggestTrades(decimal maxDrawdown, DateTime endDate, decimal baseInstrumentPrice, DateTime[] expiryDates)
        {
            //List<Option> options = new List<Option>(); //universe of options

            Option[][] ces = new Option[expiryDates.Count()][]; // 20 strikeprices on call
            Option[][] pes = new Option[expiryDates.Count()][]; // 20 strikeprices on put
            int ceIndex = 0, peIndex = 0;
            TimeSpan timeToExpiry = endDate - tradeTime;

            BS[][] ceBS = new BS[expiryDates.Count()][];
            BS[][] peBS = new BS[expiryDates.Count()][];

            int strikePriceIncremental = 100; //caculate this from the instrument

            int expiryCount = 0;
            //Iterate through all expries and all strikes, and fill greeks; current week/current month/next month
            foreach (DateTime expiry in expiryDates)
            {
                ces[expiryCount] = new Option[20];
                pes[expiryCount] = new Option[20];

                ceBS[expiryCount] = new BS[20];
                peBS[expiryCount] = new BS[20];


                calls = GetOption(expiry, baseInstrumentPrice, 260105, InstrumentType.CE); // 10 strike prices above & below
                puts = GetOption(expiry, baseInstrumentPrice, 260105, InstrumentType.PE); // 10 strike prices above & below
                bool allUpdated = UpdateLastTradePrice(calls, puts);
                if (!allUpdated)
                {
                    return;
                }


                ///TODO: Premiums of all the Calls needs to be updated from the Tick. And also price of base instrument needs to be updated and passed
                //In this DB example paas all the tokens for data streaming.

                foreach (var call in calls)
                {
                    //options[(int)InstrumentType.CE].UpdateGreeks();
                    ces[expiryCount][ceIndex] = call;
                    ceBS[expiryCount][ceIndex] = new BS(ces[expiryCount][ceIndex]);
                    ceIndex++;
                }

                foreach (var put in puts)
                {
                    //options[(int)InstrumentType.PE].UpdateGreeks();
                    pes[expiryCount][peIndex] = put;
                    peBS[expiryCount][peIndex] = new BS(pes[expiryCount][peIndex]);
                    peIndex++;
                }


                //for (int i = -10; i < 10; i++) //10 strike pricess up and low
                //{
                //    Option[] options = GetOption(expiry, atmStrike + i * strikePriceIncremental, 256265);

                //    //options[(int)InstrumentType.CE].UpdateGreeks();
                //    ces[expiryCount][ceIndex] = options[(int)InstrumentType.CE];
                //    ceBS[expiryCount][ceIndex] = new BS(ces[expiryCount][ceIndex]);

                //    //options[(int)InstrumentType.PE].UpdateGreeks();
                //    pes[expiryCount][peIndex] = options[(int)InstrumentType.PE];
                //    peBS[expiryCount][peIndex] = new BS(pes[expiryCount][peIndex]);

                //    ceIndex++;
                //    peIndex++;
                //}
                expiryCount++;
                ceIndex = 0;
                peIndex = 0;
            }


            DateTime? futureDateTime = tradeTime.AddDays(2);
            //Option[][] tradedOptions = new Option[4][];
            //tradedOptions[0] = new Option[5];
            //tradedOptions[1] = new Option[5];
            //tradedOptions[2] = new Option[5];
            //tradedOptions[3] = new Option[5];

            //int[][] parameters = new int[4][];
            //parameters[0] = new int[5];
            //parameters[1] = new int[5];
            //parameters[2] = new int[5];
            //parameters[3] = new int[5];

            Dictionary<int, Option>[] optimizedTrade;

            
            DateTime currentDateTime = tradeTime;

            decimal underlyingValue = 0;
            decimal pnl = 0, minPnl = 0;
            int index = 0;
            bool tradeFound = true;

            for (int i = 0; i < 20; i++)
                for (int j = 0; i < 20; i++)
                    for (int k = 0; i < 20; i++)
                        for (int l = 0; i < 20; i++)

                            for (int w = -3; w <= 3; w++) // Qtys should be -3,-2,-1, 0,1,2,3
                                for (int x = -3; x <= 3; x++) // Qtys should be 0,1,2,3
                                    for (int y = -3; y <= 3; y++) // Qtys should be 0,1,2,3
                                        for (int z = -3; z <= 3; z++) // Qtys should be 0,1,2,3
                                        {
                                            tradeFound = true;
                                            minPnl = 10000;
                                            for (int m = -20; m < 20; m++) //range of base instrument values for stress testing
                                            {
                                                underlyingValue = baseInstrumentPrice + m * strikePriceIncremental;
                                                //Equation to solve
                                                pnl = (GetEstimatedPrice(ceBS[0][i], futureDateTime, underlyingValue) - ces[0][i].LastPrice) * w
                                                    + (GetEstimatedPrice(ceBS[1][j], futureDateTime, underlyingValue) - ces[1][j].LastPrice) * x
                                                    + (GetEstimatedPrice(peBS[0][k], futureDateTime, underlyingValue) - pes[0][k].LastPrice) * y
                                                    + (GetEstimatedPrice(peBS[1][l], futureDateTime, underlyingValue) - pes[1][l].LastPrice) * z;
                                                
                                                if (pnl < minPnl)
                                                {
                                                    minPnl = pnl;
                                                }
                                                if (pnl < maxDrawdown)
                                                {
                                                    tradeFound = false;
                                                    break;
                                                }
                                            }
                                            if (tradeFound)
                                            {
                                                optimizedTrade = new Dictionary<int, Option>[4];

                                                optimizedTrade[0] = new Dictionary<int, Option>();
                                                optimizedTrade[0].Add(w, ces[0][i]);
                                                optimizedTrade[1] = new Dictionary<int, Option>();
                                                optimizedTrade[1].Add(x, ces[1][j]);
                                                optimizedTrade[2] = new Dictionary<int, Option>();
                                                optimizedTrade[2].Add(y, pes[0][k]);
                                                optimizedTrade[3] = new Dictionary<int, Option>();
                                                optimizedTrade[3].Add(z, pes[1][l]);

                                                optimizedTrades.Add(minPnl, optimizedTrade);
                                            }
                                        }

            //Qtys of each option can vary. Use 0 to 5 for now. Also buy or sell
            //IVs can change..so use a range such as +-10%

            //Base instrument range considered is 40*100 - 20 strike prices up and 20 down. Test on this range for max drawdown

            //for (int s = -20; s <= 20; s++) //Different strikes
            //{
            //for (int q = 0; q <= 5; q++) //Different qtys
            //{

            //}
            //}


        }

        private bool UpdateLastTradePrice(Option[] options1, Option[] options2)
        {
            bool allclear = true;
            foreach (Option o in options1)
            {
                Tick tick = null;// NCacheFacade.GetNCacheData(o.InstrumentToken);
                if (tick != null)
                {
                    o.LastPrice = tick.LastPrice;
                    o.LastTradeTime = tick.LastTradeTime?? tick.Timestamp;
                }
                else
                {
                    allclear = false;
                }
            }

            foreach (Option o in options2)
            {
                Tick tick = null;// NCacheFacade.GetNCacheData(o.InstrumentToken);
                if (tick != null)
                {
                    o.LastPrice = tick.LastPrice;
                    o.LastTradeTime = tick.LastTradeTime ?? tick.Timestamp;
                }

                else
                {
                    allclear = false;
                }
            }
            return allclear;
        }
        public decimal GetEstimatedPrice(BS bsModel, DateTime? endDateTime, decimal s)
        {
            //return bsModel.Premium(currentDateTime, endDateTime: endDateTime, assetPrice: s);
            return bsModel.FuturePremium(s,endDateTime);
        }

        /// <summary>
        /// pull instrument from the memory, instead of creating instruments
        /// </summary>
        /// <param name="expiry"></param>
        /// <param name="strike"></param>
        /// <param name="binstrumentToken"></param>
        /// <returns></returns>
        public Option[] GetOption(DateTime expiry, decimal strike, uint binstrumentToken, InstrumentType instrumentType)
        {
            DataLogic dl = new DataLogic();
            var optionsList = dl.RetrieveNextNodes(binstrumentToken, baseInstrumentPrice, instrumentType.ToString(), strike, expiry, 0, 10);
            return optionsList.Values.ToArray<Option>();
        }

        public class DuplicateKeyComparer<TKey> : IComparer<TKey> where TKey : IComparable
        {
            #region IComparer<TKey> Members

            public int Compare(TKey x, TKey y)
            {
                int result = x.CompareTo(y);

                if (result == 0)
                    return 1;   // Handle equality as beeing greater
                else
                    return result;
            }

            #endregion
        }
    }
}
