using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Algorithms.Utilities;
//using Algorithms.Utilities;
using GlobalLayer;
using Global.Web;
using Microsoft.EntityFrameworkCore.Internal;
using System.Security.Cryptography.X509Certificates;
using Newtonsoft.Json;
// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace MarketView.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PositionController : ControllerBase
    {
        public PositionController()
        {
            //System.Timers.Timer t = new System.Timers.Timer(10000);
            //t.Start();
            //t.Elapsed += T_Elapsed;
        }

        private void T_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            //ChannelUriUpdated is event 
            EventWaiter<EventArgs> ew = new EventWaiter<EventArgs>(this, "ChannelUriUpdated");
            //pushChannel.Open();
            ew.WaitForEvent(TimeSpan.FromSeconds(30));


            //HttpNotificationChannel pushChannel = new HttpNotificationChannel(channelName);
            ////ChannelUriUpdated is event 
            //EventWaiter<NotificationChannelUriEventArgs> ew = new EventWaiter<NotificationChannelUriEventArgs>(pushChannel, "ChannelUriUpdated");
            //pushChannel.Open();
            //ew.WaitForEvent(TimeSpan.FromSeconds(30));
        }

        /// <summary>
        /// Retrieve all orders by Algos
        /// </summary>
        /// <returns></returns>
        // GET: api/<PositionController>
        [HttpGet]
        public IEnumerable<IEnumerable<OrderView>> Get()
        {
            DataLogic dl = new DataLogic();
            List<Order> orders = dl.GetOrders();

            List<List<OrderView>> orderViewbyInstance = new List<List<OrderView>>();
            var algoInstances = orders.Select(x => x.AlgoInstance).Distinct();

            foreach(int ai in algoInstances)
            {
                List<OrderView> orderview = (from n in orders.FindAll(x => x.AlgoInstance == ai) select GetOrderView(n)).ToList();
                orderViewbyInstance.Add(orderview);
            }

            return orderViewbyInstance;
            //foreach (Order order in orders)
            //{
            //    if (orderViewbyInstance.ContainsKey(order.AlgoInstance))
            //    {
            //        orderViewbyInstance[order.AlgoInstance].Add(GetOrderView(order));
            //    }
            //    else
            //    {
            //        orderViewbyInstance.Add(order.AlgoInstance, new List<OrderView>() { GetOrderView(order) });
            //    }
            //}
            //JsonSerializerSettings settings = new JsonSerializerSettings();
            //settings.
            //JsonConverter converter= JsonConvert.SerializeObject()

            //return JsonConvert.SerializeObject(orderViewbyInstance);
        }
        private OrderView GetOrderView(Order order)
        {
            return new OrderView
            {
                instrumenttoken = order.InstrumentToken,
                tradingsymbol = order.Tradingsymbol.Trim(' '),
                transactiontype = order.TransactionType,
                status = order.Status,
                statusmessage = order.StatusMessage,
                price = order.AveragePrice,
                quantity = order.Quantity,
                triggerprice = order.TriggerPrice,
                algorithm = Convert.ToString((AlgoIndex)order.AlgoIndex),
                algoinstance = order.AlgoInstance,
                ordertime = order.OrderTimestamp.GetValueOrDefault(DateTime.Now).ToString("yyyy-MM-dd HH:mm:ss"),
                ordertype = Convert.ToString(order.OrderType)
            };
        }

        // GET api/<PositionController>/5
        [HttpGet("{id}")]
        public async Task GetStockUpdate(int id)
        {
            //Response.Headers.Add("Content-Type", "text/event-stream;;charset=utf-8");
            //Response.Headers.Add("Cache-Control", "no-cache");

            //LogData log = new LogData() {
            //    TimeStamp = DateTime.Now.ToString("HH:mm:ss") + "\n",
            //    Event = "timestamp\n",
            //    Data = DateTime.Now.ToString("HH:mm:ss") + "\n\n"
            //};
            ////StringBuilder sb = new StringBuilder("event: " + "timestamp\n");
            ////sb.Append($"data: " + DateTime.Now.ToString("HH:mm:ss") + "\n\n");

            //string resp = JsonConvert.SerializeObject(log);

            ////sb.Append($"data: JSON.stringify(response.data) \n\n");

            //byte[] dataItemBytes = UTF8Encoding.UTF8.GetBytes(resp);

            //await Response.Body.WriteAsync(dataItemBytes, 0, dataItemBytes.Length);
            //await Response.Body.FlushAsync();
            ////return "value";

            string[] data = new string[] {
        "Hello World!",
        "Hello Galaxy!",
        "Hello Universe!"
            };

            Response.Headers.Add("Content-Type",
        "text/event-stream");

            for (int i = 0; i < data.Length; i++)
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
                string dataItem = $"data: {data[i]}\n\n";
                byte[] dataItemBytes =
        ASCIIEncoding.ASCII.GetBytes(dataItem);
                await Response.Body.WriteAsync
        (dataItemBytes, 0, dataItemBytes.Length);
                await Response.Body.FlushAsync();
            }
        }

        // POST api/<PositionController>
        [HttpPost]
        public void Post([FromBody] string value)
        {
        }

        // PUT api/<PositionController>/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/<PositionController>/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
