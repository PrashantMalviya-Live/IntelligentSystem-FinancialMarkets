using Algorithms.Algorithms;
using Algorithms.Utilities;
using GlobalLayer;
using Global.Web;
using GlobalCore;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZMQFacade;
using System.Data;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace TradeEMACross.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MomentumVolumeController : ControllerBase
    {
        ZMQClient zmqClient;
        List<OptionVolumeRateEMAThreshold> activeAlgoObjects;
        public MomentumVolumeController()
        {
            activeAlgoObjects = new List<OptionVolumeRateEMAThreshold>();
           // GetActiveAlgos();
        }

        
        [HttpGet]
        public IEnumerable<BInstumentView> Get()
        {
            DataLogic dl = new DataLogic();
            List<Instrument> bInstruments = dl.RetrieveBaseInstruments();

            return (from n in bInstruments select new BInstumentView { InstrumentToken = n.InstrumentToken, TradingSymbol = n.TradingSymbol.Trim(' ') }).ToList();
        }


        [HttpGet("activealgos")]
        public async Task <IEnumerable<ActiveAlgosView>> GetActiveAlgos()
        {
            DataLogic dl = new DataLogic();
            DataSet ds = dl.GetActiveAlgos(AlgoIndex.MomentumTrade_Option);
            
            List<ActiveAlgosView> activeAlgos = new List<ActiveAlgosView>();

            DataTable dtActiveAlgos = ds.Tables[0];
            DataRelation algo_orders_relation = ds.Relations.Add("Algo_Orders", new DataColumn[] { ds.Tables[0].Columns["Id"] },
                new DataColumn[] { ds.Tables[2].Columns["AlgoInstance"] });

            foreach (DataRow drAlgo in dtActiveAlgos.Rows)
            {
                ActiveAlgosView algosView = new ActiveAlgosView();

                OptionMomentumInput algoInput = new OptionMomentumInput
                {
                    Expiry = Convert.ToDateTime(drAlgo["Expiry"]),
                    CTF = Convert.ToInt32(drAlgo["CandleTimeFrame_Mins"]),
                    Quantity = Convert.ToInt32(drAlgo["InitialQtyInLotSize"]),
                    Token = Convert.ToUInt32(drAlgo["BToken"])
                };
                algosView.Aid = Convert.ToInt32(drAlgo["AlgoId"]);
                algosView.AN = Convert.ToString((AlgoIndex)algosView.Aid);
                algosView.AIns = Convert.ToInt32(drAlgo["Id"]);

                foreach (DataRow drOrder in drAlgo.GetChildRows(algo_orders_relation))
                {
                    Order o = GetOrder(drOrder);
                    if(o.Status == Constants.ORDER_STATUS_OPEN && o.OrderType == Constants.ORDER_TYPE_SLM)
                    {
                        algoInput.ActiveOrder = o;
                    }
                    algosView.Orders.Add(GetOrderView(o));
                }
                if (algoInput.ActiveOrder != null)
                {
                    Trade(algoInput);
                }
                activeAlgos.Add(algosView);
            }
            return activeAlgos.ToArray();
        }
        
        // GET api/<HomeController>/5
        [HttpGet("{token}")]
        public IEnumerable<string> OptionExpiries(uint token)
        {
            DataLogic dl = new DataLogic();
            List<string> expiryList = dl.RetrieveOptionExpiries(token);
            return expiryList;
        }


        [HttpGet("{token}/{expiry}")]
        //[Route("GetOptions")]
        public IEnumerable<OptionView> GetOptions(uint token, string expiry)
        {
            //Retrieve expiry list ONLY here and fulllist at expiry click
            DataLogic dl = new DataLogic();
            List<Instrument> optionsList = dl.RetrieveOptions(token,
                Convert.ToDateTime(expiry));

            return (from n in optionsList select new OptionView { InstrumentToken = n.InstrumentToken, TradingSymbol = n.TradingSymbol.Trim(' '), Type = n.InstrumentType.Trim(' '), Strike = n.Strike }).ToList();
        }

        [HttpPost]
        public async Task<ActiveAlgosView> Trade([FromBody] OptionMomentumInput optionMomentumInput)
        {
            uint instrumentToken = optionMomentumInput.Token;
            DateTime endDateTime = DateTime.Now;
            TimeSpan candleTimeSpan = TimeSpan.FromMinutes(optionMomentumInput.CTF);

            DateTime? expiry = optionMomentumInput.Expiry; // Convert.ToDateTime("2020-10-01");
            //decimal strikePriceRange = 1;
            int optionQuantity = optionMomentumInput.Quantity;

#if local
            endDateTime = Convert.ToDateTime("2020-10-12 09:15:00");
#endif

            ///FOR ALL STOCKS FUTURE , PASS INSTRUMENTTOKEN AS ZERO. FOR CE/PE ON BNF/NF SEND THE INDEX TOKEN AS INSTRUMENTTOKEN
            OptionVolumeRateEMAThreshold volumeThreshold = new OptionVolumeRateEMAThreshold(endDateTime, candleTimeSpan, instrumentToken, expiry, optionQuantity);

            Order activeOrder = optionMomentumInput.ActiveOrder;
            if (activeOrder != null)
            {
                volumeThreshold.LoadActiveOrders(activeOrder);
            }
            
            volumeThreshold.OnOptionUniverseChange += VolumeThreshold_OnOptionUniverseChange;
            volumeThreshold.OnTradeEntry += VolumeThreshold_OnTradeEntry;
            volumeThreshold.OnTradeExit += VolumeThreshold_OnTradeExit; 
            
            activeAlgoObjects.Add(volumeThreshold);


            Task task = Task.Run(() => NMQClientSubscription(volumeThreshold, instrumentToken));

            //await task;
            return new ActiveAlgosView { Aid = Convert.ToInt32(AlgoIndex.MomentumTrade_Option), 
                AN = Convert.ToString((AlgoIndex)AlgoIndex.MomentumTrade_Option), AIns = volumeThreshold.AlgoInstance };
            
        }

        [HttpGet("dummyorder")]
        public void GetDummyOrder()
        {
            LoggerCore.PublishLog(56, AlgoIndex.MomentumTrade_Option, LogLevel.Health, DateTime.UtcNow, "1", "GetDummyOrder");
        }
        private async Task NMQClientSubscription(OptionVolumeRateEMAThreshold volumeThreshold, uint token)
        {
            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(new List<uint>() { token });

            await zmqClient.Subscribe(volumeThreshold);
        }

        private void VolumeThreshold_OnTradeExit(Order st)
        {
            //publish trade details and count
            //Bind with trade token details, use that as an argument
            OrderCore.PublishOrder(st);
        }

        private void VolumeThreshold_OnTradeEntry(Order st)
        {
            //publish trade details and count

            OrderCore.PublishOrder(st);

        }

        [HttpPut("{id}")]
        public void StopTrade(int id)
        {
            OptionVolumeRateEMAThreshold algo = activeAlgoObjects.First(x => x.AlgoInstance == id);
            algo.StopTrade();
        }

        private void VolumeThreshold_OnOptionUniverseChange(OptionVolumeRateEMAThreshold source)
        {
            try
            {
                zmqClient.AddSubscriber(source.SubscriptionTokens);
            }
            catch (Exception ex)
            {
                throw ex;

            }
        }
        private Order GetOrder(DataRow drOrders)
        {
            return new Order
            {
                OrderId = Convert.ToString(drOrders["OrderId"]),
                InstrumentToken = Convert.ToUInt32(drOrders["InstrumentToken"]),
                Tradingsymbol = (string)drOrders["TradingSymbol"],
                TransactionType = (string)drOrders["TransactionType"],
                AveragePrice = Convert.ToDecimal(drOrders["AveragePrice"]),
                Quantity = (int)drOrders["Quantity"],
                TriggerPrice = Convert.ToDecimal(drOrders["TriggerPrice"]),
                Status = (string)drOrders["Status"],
                StatusMessage = Convert.ToString(drOrders["StatusMessage"]),
                OrderType = Convert.ToString(drOrders["OrderType"]),
                OrderTimestamp = Convert.ToDateTime(drOrders["OrderTimeStamp"]),
                AlgoIndex = Convert.ToInt32(drOrders["AlgoIndex"]),
                AlgoInstance = Convert.ToInt32(drOrders["AlgoInstance"])
            };
        }
        private OrderView GetOrderView(Order order)
        {
            return new OrderView
            {
                OrderId = order.OrderId,
                InstrumentToken = order.InstrumentToken,
                TradingSymbol = order.Tradingsymbol.Trim(' '),
                TransactionType = order.TransactionType,
                Status = order.Status,
                StatusMessage = order.StatusMessage,
                Price = order.AveragePrice,
                Quantity = order.Quantity,
                TriggerPrice = order.TriggerPrice,
                Algorithm = Convert.ToString((AlgoIndex)order.AlgoIndex),
                AlgoInstance = order.AlgoInstance,
                OrderTime = order.OrderTimestamp.GetValueOrDefault(DateTime.Now).ToString("yyyy-MM-dd HH:mm:ss"),
                OrderType = Convert.ToString(order.OrderType)
            };
        }

        //[HttpPost]
        public async Task GetLog()//[FromBody] int algoInstance)
        {
            //for (int i = 0; i < 10000; i++)
            //{
            //int i = 126;
            //    LoggerCore.PublishLog(new LogData { AlgoInstance = 1, Level = Global.LogLevel.Debug, 
            //        LogTime = DateTime.UtcNow, Message = String.Format("Momentum Trade...{0}",i), SourceMethod = "Momentum" });

            //LoggerCore.PublishLog(new LogData
            //{
            //    AlgoInstance = 0,
            //    Level = GlobalLayer.LogLevel.Error,
            //    LogTime = DateTime.UtcNow,
            //    Message = String.Format("Strangle Trade...{0}", i),
            //    SourceMethod = "Strangle"
            //});
            //}

            // LoggerService ls = new LoggerService(LoggerRepository);
            // LoggerRepository.AddLog(new LogData { AlgoInstance = 1, Level = Global.LogLevel.Debug, LogTime = DateTime.UtcNow, Message = "Dummy text", SourceMethod = "Momentum" });
            //ls.PublishLog(new LogData { AlgoInstance = 1, Level = Global.LogLevel.Debug, LogTime = DateTime.UtcNow, Message = "Dummy text", SourceMethod = "Momentum" });
            //DataLogic dl = new DataLogic();
            //List<Instrument> bInstruments = dl.RetrieveBaseInstruments();

            //return (from n in bInstruments select new BInstumentView { InstrumentToken = n.InstrumentToken, TradingSymbol = n.TradingSymbol.Trim(' ') }).ToList();
        }

    }
}
