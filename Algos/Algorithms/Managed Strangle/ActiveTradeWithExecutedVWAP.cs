using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AdvanceAlgos.Utilities;
//using Confluent.Kafka;
//using KafkaStreams;
using Global;
using KiteConnect;
using ZConnectWrapper;
using Pub_Sub;
//using MarketDataTest;
using System.Data;

namespace AdvanceAlgos.Algorithms
{
    public class ActiveTradeWithExecutedVWAP : IObserver<Tick[]>
    {
        public IDisposable UnsubscriptionToken;
        List<StrangleData> ActiveStrangles = new List<StrangleData>();
        public int rowCounter = 1;

        public virtual void OnNext(Tick[] ticks)
        {
            lock (ActiveStrangles)
            {
                for (int i = 0; i < ActiveStrangles.Count; i++)
                {
                    ReviewStrangle(ActiveStrangles.ElementAt(i), ticks);
                }
            }
        }

        private void ReviewStrangle(StrangleData strangleNode, Tick[] ticks)
        {
            Tick baseInstrumentTick = ticks.FirstOrDefault(x => x.InstrumentToken == strangleNode.BaseInstrumentToken);
            if (baseInstrumentTick.LastPrice != 0)
            {
                strangleNode.BaseInstrumentPrice = baseInstrumentTick.LastPrice;
            }
            if (strangleNode.BaseInstrumentPrice == 0)
            {
                return;
            }

            //Get Max Pain
            decimal maxPainStrike = UpdateMaxPainStrike(strangleNode, ticks);
            if (maxPainStrike == 0)
            {
                return;
            }

            TradedOption callOption = strangleNode.CurrentCall;
            TradedOption putOption = strangleNode.CurrentPut;

            //Initial sell
            if (callOption.TradingStatus == PositionStatus.NotTraded && putOption.TradingStatus == PositionStatus.NotTraded)
            {
                strangleNode.MaxPainStrike = maxPainStrike;
                NewPositions(callOption, putOption, strangleNode, ticks[0].Timestamp);
            }

            decimal currentPnL = strangleNode.NetPnL;

            callOption.Option = strangleNode.Calls[callOption.Option.Strike];
            putOption.Option = strangleNode.Puts[putOption.Option.Strike];

            Instrument currentCall = callOption.Option;//strangleNode.Calls.ElementAt(callOption.Index).Value;
            Instrument currentPut = putOption.Option; //strangleNode.Puts.ElementAt(putOption.Index).Value;

            if (currentCall.LastPrice == 0 || currentPut.LastPrice == 0) return;
            currentPnL -= (currentCall.LastPrice + currentPut.LastPrice);

            //WriteToExcel(++rowCounter, ticks[0].Timestamp.Value.ToString(), currentPnL.ToString());
            WriteToFile(ticks[0].Timestamp.Value.ToString(), currentPnL.ToString());

            //Operational check for PnL
            if (callOption.TradingStatus == PositionStatus.Open && putOption.TradingStatus == PositionStatus.Open)
            {
                //if loss is more than threshold or profit target is met, close the trade
                //if (strangleNode.NetPnL > 0 && (currentPnL * -1 > strangleNode.MaxLossThreshold || currentPnL > strangleNode.ProfitTarget))
                if (strangleNode.NetPnL > 0 && (currentPnL * -1 > strangleNode.MaxLossThreshold || currentPnL > strangleNode.ProfitTarget))
                {
                    ClosePositions(currentCall, currentPut, callOption, putOption, strangleNode, ticks[0].Timestamp);
                }
                //if (strangleNode.MaxPainStrike != maxPainStrike)
                if (strangleNode.MaxPainStrike > maxPainStrike + 105 || strangleNode.MaxPainStrike < maxPainStrike - 105) //Putting a range for max pain shift
                {
                    ClosePositions(currentCall, currentPut, callOption, putOption, strangleNode, ticks[0].Timestamp);
                    callOption.TradingStatus = PositionStatus.NotTraded;
                    putOption.TradingStatus = PositionStatus.NotTraded;
                }
            }
            else if (callOption.TradingStatus == PositionStatus.Closed && putOption.TradingStatus == PositionStatus.Closed)
            {
                //if (strangleNode.MaxPainStrike != maxPainStrike)
                if (strangleNode.MaxPainStrike > maxPainStrike + 105 || strangleNode.MaxPainStrike < maxPainStrike - 105) //Putting a range for max pain shift
                {
                    strangleNode.MaxPainStrike = maxPainStrike;
                    NewPositions(callOption, putOption, strangleNode, ticks[0].Timestamp);
                }
            }
        }
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


        public virtual void Unsubscribe()
        {
            UnsubscriptionToken.Dispose();
        }

        public virtual void OnCompleted()
        {
        }
    }
}
