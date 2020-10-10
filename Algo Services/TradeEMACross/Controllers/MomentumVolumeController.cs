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
            GetActiveAlgos();
            
            //volumeThreshold = new OptionVolumeRateEMAThreshold(endDateTime, candleTimeSpan, instrumentToken, expiry, optionQuantity);
            //volumeThreshold.OnOptionUniverseChange += VolumeThreshold_OnOptionUniverseChange;
            //volumeThreshold.OnTradeEntry += VolumeThreshold_OnTradeEntry;
            //volumeThreshold.OnTradeExit += VolumeThreshold_OnTradeExit; ;


            //zmqClient = new ZMQClient();
            //zmqClient.AddSubscriber(new List<uint>() { instrumentToken });

            //zmqClient.Subscribe(volumeThreshold);
        }

        
        [HttpGet]
        public IEnumerable<BInstumentView> Get()
        {
            DataLogic dl = new DataLogic();
            List<Instrument> bInstruments = dl.RetrieveBaseInstruments();

            return (from n in bInstruments select new BInstumentView { InstrumentToken = n.InstrumentToken, TradingSymbol = n.TradingSymbol.Trim(' ') }).ToList();
        }


        [HttpGet("activealgos")]
        public IEnumerable<IEnumerable<OrderView>> GetActiveAlgos()
        {
            DataLogic dl = new DataLogic();
            DataSet ds = dl.GetActiveAlgos(AlgoIndex.VolumeThreshold);
            List<Order> orders = DataTableToOrderList(ds.Tables[2]);

            List<List<OrderView>> orderViewbyInstance = new List<List<OrderView>>();
            var algoInstances = orders.Select(x => x.AlgoInstance).Distinct();

            Dictionary<uint, OrderLevels> activeOrders = new Dictionary<uint, OrderLevels>();

            foreach (int ai in algoInstances)
            {
                List<OrderView> orderview = (from n in orders.FindAll(x => x.AlgoInstance == ai) select GetOrderView(n)).ToList();
                orderViewbyInstance.Add(orderview);
            }

            DataTable dtActiveAlgos = ds.Tables[0];
            
            foreach(DataRow dr in dtActiveAlgos.Rows)
            {
                OptionMomentumInput algoInput = new OptionMomentumInput
                {
                    Expiry = Convert.ToDateTime(dr["Expiry"]),
                    CTF = Convert.ToInt32(dr["CandleTimeFrame_Mins"]),
                    Quantity = Convert.ToInt32(dr["InitialQtyInLotSize"]),
                    Token = Convert.ToUInt32(dr["BToken"])
                };

                Order order = orders.FirstOrDefault(x => x.Status == Constants.ORDER_STATUS_OPEN && x.OrderType == Constants.ORDER_TYPE_SLM);

                Trade(algoInput, order);
            }

            return orderViewbyInstance;
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
        public void Trade([FromBody] OptionMomentumInput optionMomentumInput, Order activeOrder = null)
        {
            uint instrumentToken = optionMomentumInput.Token;
            DateTime endDateTime = DateTime.Now;
            TimeSpan candleTimeSpan = TimeSpan.FromMinutes(optionMomentumInput.CTF);

            DateTime? expiry = optionMomentumInput.Expiry; // Convert.ToDateTime("2020-10-01");
            //decimal strikePriceRange = 1;
            int optionQuantity = optionMomentumInput.Quantity;

#if local
            endDateTime = Convert.ToDateTime("2020-10-09 09:15:00");
#endif

            ///FOR ALL STOCKS FUTURE , PASS INSTRUMENTTOKEN AS ZERO. FOR CE/PE ON BNF/NF SEND THE INDEX TOKEN AS INSTRUMENTTOKEN
            OptionVolumeRateEMAThreshold volumeThreshold = new OptionVolumeRateEMAThreshold(endDateTime, candleTimeSpan, instrumentToken, expiry, optionQuantity);

            if (activeOrder != null)
            {
                volumeThreshold.LoadActiveOrders(activeOrder);
            }
            
            volumeThreshold.OnOptionUniverseChange += VolumeThreshold_OnOptionUniverseChange;
            volumeThreshold.OnTradeEntry += VolumeThreshold_OnTradeEntry;
            volumeThreshold.OnTradeExit += VolumeThreshold_OnTradeExit; ;


            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(new List<uint>() { instrumentToken });

            zmqClient.Subscribe(volumeThreshold);

            activeAlgoObjects.Add(volumeThreshold);
        }

        private void VolumeThreshold_OnTradeExit(Order st)
        {
            //publish trade details and count
            //Bind with trade token details, use that as an argument
        }

        private void VolumeThreshold_OnTradeEntry(Order st)
        {
            //publish trade details and count
            //Bind with trade token details, use that as an argument

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
        private List<Order> DataTableToOrderList(DataTable dtOrders)
        {
            List<Order> orders = new List<Order>();
            foreach (DataRow dr in dtOrders.Rows)
            {
                Order order = new Order
                {
                    InstrumentToken = Convert.ToUInt32(dr["InstrumentToken"]),
                    Tradingsymbol = (string)dr["TradingSymbol"],
                    TransactionType = (string)dr["TransactionType"],
                    AveragePrice = Convert.ToDecimal(dr["AveragePrice"]),
                    Quantity = (int)dr["Quantity"],
                    TriggerPrice = Convert.ToDecimal(dr["TriggerPrice"]),
                    Status = (string)dr["Status"],
                    StatusMessage = Convert.ToString(dr["StatusMessage"]),
                    OrderType = Convert.ToString(dr["OrderType"]),
                    OrderTimestamp = Convert.ToDateTime(dr["OrderTimeStamp"]),
                    AlgoIndex = Convert.ToInt32(dr["AlgoIndex"]),
                    AlgoInstance = Convert.ToInt32(dr["AlgoInstance"])
                };
                orders.Add(order);
            }
            return orders;
        }
        private OrderView GetOrderView(Order order)
        {
            return new OrderView
            {
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
            int i = 126;
            //    await LoggerCore.PublishLog(new LogData { AlgoInstance = 1, Level = Global.LogLevel.Debug, 
            //        LogTime = DateTime.UtcNow, Message = String.Format("Momentum Trade...{0}",i), SourceMethod = "Momentum" });

            await LoggerCore.PublishLog(new LogData
            {
                AlgoInstance = 0,
                Level = GlobalLayer.LogLevel.Error,
                LogTime = DateTime.UtcNow,
                Message = String.Format("Strangle Trade...{0}", i),
                SourceMethod = "Strangle"
            });
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
