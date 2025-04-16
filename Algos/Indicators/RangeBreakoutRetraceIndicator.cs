using System;
using System.Collections.Generic;
using System.ComponentModel;
using Algorithms.Utilities;
using GlobalLayer;
using Google.Protobuf.WellKnownTypes;

namespace Algorithms.Indicators
{
    /// <summary>
    /// The part of the indicator <see cref="DirectionalIndex"/>.
    /// </summary>
    [IndicatorIn(typeof(CandleIndicatorValue))]
    public class RangeBreakoutRetraceIndicator : BaseIndicator
    {
        public PriceRange CurrentPriceRange { get; set; }
        public PriceRange PreviousPriceRange { get; set; }

        public Breakout Breakout { get; set; }
        public uint InstrumentToken { get; set; }

        public string InstrumentSymbol { get; set; }

        private int _currentCandleIndex = 0;

        private bool _indicatorChanged = false;

        public RangeBreakoutRetraceIndicator()
        {
            IsFormed = false;
        }

        public int TimeSpan { get; set; }

        protected override IIndicatorValue OnProcess(IIndicatorValue input)
        {
            //var candle = input.GetValue<Candle>();

            var candle = (input as CandleIndicatorValue).Value;

            Breakout b = UpdatePriceRange(candle, _currentCandleIndex++, out _indicatorChanged);
            
            
            

            return new BreakOutIndicatorValue(this, b) { IsEmpty = !_indicatorChanged };
        }


        //private void DetermineCurrentRange(uint token, DateTime currentTime)
        //{
        //    for (int c = 0; c < Container.Count; c++)
        //    {
        //        Candle tc = Container.GetValue(GetValue(c));

        //        UpdatePriceRange(tc, c, token);
        //    }
        //}

        //private decimal GetLowerRange(int initialIndex, int finalindex, decimal currentCandleLowPrice)
        //{
        //    decimal lowerRange = 0;

        //    finalindex = finalindex - 1; // This is done becuase container value gets added AFTER the onprocess method. So the current candle value is taken seperately.
        //    initialIndex = finalindex - initialIndex;

        //    for (int c = 0; c < initialIndex; c++)
        //    {
        //        CandleIndicatorValue tc = Container.GetValue(c).Item1 as CandleIndicatorValue;

        //        if (tc.Value.LowPrice < lowerRange || lowerRange == 0)
        //        {
        //            lowerRange = tc.Value. LowPrice;
        //        }
        //    }
        //    return Math.Min(currentCandleLowPrice, lowerRange);
        //}
        //private decimal GetUpperRange(int initialIndex, int finalindex, decimal currentCandleHighPrice)
        //{
        //    decimal upperRange = 0;

        //    finalindex = finalindex - 1; // This is done becuase container value gets added AFTER the onprocess method. So the current candle value is taken seperately.

        //    initialIndex = finalindex - initialIndex;

        //    for (int c = 0; c < initialIndex; c++)
        //    {
        //        CandleIndicatorValue tc = Container.GetValue(c).Item1 as CandleIndicatorValue;

        //        if (tc.Value.HighPrice > upperRange)
        //        {
        //            upperRange = tc.Value.HighPrice;
        //        }
        //    }
        //    return Math.Max(currentCandleHighPrice, upperRange);

        //}


        private decimal GetUpperRange(int initialIndex, int finalindex, decimal currentCandleHighPrice)
        {
            decimal upperRange = 0;

            int i = 0;
            while(true)
            {
                CandleIndicatorValue tc = Container.GetValue(i).Item1 as CandleIndicatorValue;
                BreakOutIndicatorValue bc = Container.GetValue(i).Item2 as BreakOutIndicatorValue;

                if (tc.Value.HighPrice > upperRange)
                {
                    upperRange = tc.Value.HighPrice;
                }

                if (i >= Container.Count - 2)
                {
                    break;
                }
                BreakOutIndicatorValue pbc = Container.GetValue(i + 1).Item2 as BreakOutIndicatorValue;

                //if(bc.Value == Breakout.DOWN && pbc.Value == Breakout.NONE)
                //{
                //    break;
                //}
                if (bc.Value == Breakout.NONE && pbc.Value != Breakout.NONE)
                {
                    break;
                }
                i++;
            }

            // This is done becuase container value gets added AFTER the onprocess method. So the current candle value is taken seperately.
            return Math.Max(currentCandleHighPrice, upperRange); 

        }
        private decimal GetLowerRange(int initialIndex, int finalindex, decimal currentCandleLowPrice)
        {
            decimal lowerRange = 0;

            int i = 0;
            while (true)
            {
                CandleIndicatorValue tc = Container.GetValue(i).Item1 as CandleIndicatorValue;
                BreakOutIndicatorValue bc = Container.GetValue(i).Item2 as BreakOutIndicatorValue;

                if (tc.Value.LowPrice < lowerRange || lowerRange == 0)
                {
                    lowerRange = tc.Value.LowPrice;
                }

                if (i >= Container.Count - 2)
                {
                    break;
                }
                BreakOutIndicatorValue pbc = Container.GetValue(i + 1).Item2 as BreakOutIndicatorValue;

                //if (bc.Value == Breakout.UP && pbc.Value == Breakout.NONE)
                //{
                //    break;
                //}
                if (bc.Value == Breakout.NONE && pbc.Value != Breakout.NONE)
                {
                    break;
                }

                i++;
            }

            // This is done becuase container value gets added AFTER the onprocess method. So the current candle value is taken seperately.
            return Math.Min(currentCandleLowPrice, lowerRange);

        }

        private Breakout UpdatePriceRange(Candle tc, int currentCandleIndex, out bool indicatorChanged)
        {
            Breakout breakout = Breakout.NONE;// CurrentBreakOutMode;
            CurrentPriceRange ??= new PriceRange();
            PreviousPriceRange ??= new PriceRange();
            indicatorChanged = false;

            if (CurrentPriceRange.Upper == 0)
            {
                CurrentPriceRange.Upper = tc.HighPrice;
                CurrentPriceRange.NextUpper = tc.HighPrice;
                CurrentPriceRange.UpperCandleIndex = currentCandleIndex;
                breakout = Breakout.NONE;
            }
            else if (tc.ClosePrice > CurrentPriceRange.NextUpper)
            {
                //CopyPriceRange(CurrentPriceRange, PreviousPriceRange);
                
                //iterate through last breakout and this breakout to find lower range;
                if (currentCandleIndex != CurrentPriceRange.UpperCandleIndex + 1)
                {
                    CopyPriceRange(CurrentPriceRange, PreviousPriceRange);

                    CurrentPriceRange.Lower = GetLowerRange(CurrentPriceRange.UpperCandleIndex, currentCandleIndex, tc.LowPrice);
                    CurrentPriceRange.NextLower = CurrentPriceRange.Lower;


                    breakout = Breakout.UP;

                    IsFormed = true;
                }
                CurrentPriceRange.UpperCandleIndex = currentCandleIndex;

                CurrentPriceRange.Upper = tc.HighPrice;
                CurrentPriceRange.NextUpper = tc.HighPrice;

                
                
                indicatorChanged = true;
                
                //DataLogic dl = new DataLogic();
                //dl.UpdateAlgoParamaters(_algoInstance, arg1: Convert.ToInt32(breakout), upperLimit: CurrentPriceRange.Upper, lowerLimit: CurrentPriceRange.Lower,
                //    arg2: CurrentPriceRange.NextUpper, arg3: CurrentPriceRange.NextLower, arg4: PreviousPriceRange.Upper, arg5: PreviousPriceRange.Lower,
                //    arg6: PreviousPriceRange.NextUpper, arg7: CurrentPriceRange.NextLower);
            }
            else if (tc.HighPrice > CurrentPriceRange.NextUpper)
            {
                //CopyPriceRange(CurrentPriceRange, PreviousPriceRange);
                CurrentPriceRange.NextUpper = tc.HighPrice;
                //CurrentPriceRange.UpperCandleIndex = currentCandleIndex;
                //CurrentBreakOutMode = Breakout.NONE;
                breakout = Breakout.NONE;
                indicatorChanged = true;
                
                //DataLogic dl = new DataLogic();
                //dl.UpdateAlgoParamaters(_algoInstance, arg1: Convert.ToInt32(breakout), upperLimit: CurrentPriceRange.Upper, lowerLimit: CurrentPriceRange.Lower,
                //    arg2: CurrentPriceRange.NextUpper, arg3: CurrentPriceRange.NextLower, arg4: PreviousPriceRange.Upper, arg5: PreviousPriceRange.Lower,
                //    arg6: PreviousPriceRange.NextUpper, arg7: CurrentPriceRange.NextLower);
            }


            if (CurrentPriceRange.Lower == 0)
            {
                CurrentPriceRange.Lower = tc.LowPrice;
                CurrentPriceRange.NextLower = tc.LowPrice;
                CurrentPriceRange.LowerCandleIndex = currentCandleIndex;
                breakout = Breakout.NONE;
            }
            else if (tc.ClosePrice < CurrentPriceRange.NextLower)
            {
                //CopyPriceRange(CurrentPriceRange, PreviousPriceRange);
                
                //iterate through last breakout and this breakout to find lower range;
                if (currentCandleIndex != CurrentPriceRange.LowerCandleIndex + 1)
                {
                    CopyPriceRange(CurrentPriceRange, PreviousPriceRange);
                    CurrentPriceRange.Upper = GetUpperRange(CurrentPriceRange.LowerCandleIndex, currentCandleIndex, tc.HighPrice);
                    CurrentPriceRange.NextUpper = CurrentPriceRange.Upper;


                    breakout = Breakout.DOWN;

                    IsFormed = true;

                }
                CurrentPriceRange.LowerCandleIndex = currentCandleIndex;

                CurrentPriceRange.Lower = tc.LowPrice;
                CurrentPriceRange.NextLower = tc.LowPrice;

                indicatorChanged = true;

                //breakout = Breakout.DOWN;
                //indicatorChanged = true;
                //DataLogic dl = new DataLogic();
                //dl.UpdateAlgoParamaters(_algoInstance, arg1: Convert.ToInt32(breakout), upperLimit: CurrentPriceRange.Upper, lowerLimit: CurrentPriceRange.Lower,
                //   arg2: CurrentPriceRange.NextUpper, arg3: CurrentPriceRange.NextLower, arg4: PreviousPriceRange.Upper, arg5: PreviousPriceRange.Lower,
                //   arg6: PreviousPriceRange.NextUpper, arg7: CurrentPriceRange.NextLower);

            }
            else if (tc.LowPrice < CurrentPriceRange.NextLower)
            {
                //CopyPriceRange(CurrentPriceRange, PreviousPriceRange);
                //CurrentPriceRange.Lower = tc.LowPrice;
                CurrentPriceRange.NextLower = tc.LowPrice;
                //CurrentPriceRange.LowerCandleIndex = currentCandleIndex;
                //CurrentBreakOutMode = Breakout.NONE;
                breakout = Breakout.NONE;
                indicatorChanged = true;


                //DataLogic dl = new DataLogic();
                //dl.UpdateAlgoParamaters(_algoInstance, arg1: Convert.ToInt32(breakout), upperLimit: CurrentPriceRange.Upper, lowerLimit: CurrentPriceRange.Lower,
                //   arg2: CurrentPriceRange.NextUpper, arg3: CurrentPriceRange.NextLower, arg4: PreviousPriceRange.Upper, arg5: PreviousPriceRange.Lower,
                //   arg6: PreviousPriceRange.NextUpper, arg7: CurrentPriceRange.NextLower);
            }

            return breakout;
        }

        public decimal GetUpperBreakoutPriceValue()
        {
            return PreviousPriceRange.Upper;
        }
        public decimal GetLowerBreakoutPriceValue()
        {
            return PreviousPriceRange.Lower;
        }

        public decimal GetCurrentValue<T>()
        {
            decimal currentValue;
            switch (Breakout)
            {
                case Breakout.DOWN:
                    currentValue = PreviousPriceRange.Lower;
                    break;
                case Breakout.UP:
                    currentValue = PreviousPriceRange.Upper;
                    break;
                default:
                    currentValue = PreviousPriceRange.Upper;
                    break;
            }
            
            return currentValue;
        }

        //public decimal GetValue<T>()
        //{
        //    decimal currentValue;
        //    switch (Breakout)
        //    {
        //        case Breakout.DOWN:
        //            currentValue = PreviousPriceRange.Lower;
        //            break;
        //        case Breakout.UP:
        //            currentValue = PreviousPriceRange.Upper;
        //            break;
        //        default:
        //            currentValue = PreviousPriceRange.Upper;
        //            break;
        //    }

        //    return currentValue;
        //}
        public T GetValue<T>()
        {
            decimal currentValue;
            switch (Breakout)
            {
                case Breakout.DOWN:
                    currentValue = PreviousPriceRange.Lower;
                    break;
                case Breakout.UP:
                    currentValue = PreviousPriceRange.Upper;
                    break;
                default:
                    currentValue = PreviousPriceRange.Upper;
                    break;
            }

            return  (T)Convert.ChangeType(currentValue, typeof(decimal))  ;
        }
        private void CopyPriceRange(PriceRange fromPriceRange, PriceRange toPriceRange)
        {
            toPriceRange.Upper = fromPriceRange.Upper;
            toPriceRange.Lower = fromPriceRange.Lower;
            toPriceRange.NextUpper = fromPriceRange.NextUpper;
            toPriceRange.NextLower = fromPriceRange.NextLower;
            toPriceRange.UpperCandleIndex = fromPriceRange.UpperCandleIndex;
            toPriceRange.LowerCandleIndex = fromPriceRange.LowerCandleIndex;
        }
    }
}