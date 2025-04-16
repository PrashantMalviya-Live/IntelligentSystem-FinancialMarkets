using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using GlobalLayer;
using Algos.TLogics;
using Algorithms.Utilities;
using ZMQFacade;
//using ZeroMQ;
using System.Data;

namespace DeltaManagerService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DeltaController : ControllerBase
    {
        [HttpGet("positions/all")]
        //[HttpGet("positions/{id}")]
        public IEnumerable<AlgoPosition> GetCurrentPositions()
        {
            DataLogic dl = new DataLogic();
            DataSet dsActiveStrangles = dl.RetrieveActiveStrangles(AlgoIndex.ExpiryTrade);

            return null;


            //var rng = new Random();
            //return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            //{
            //    Date = DateTime.Now.AddDays(index),
            //    TemperatureC = rng.Next(-20, 55),
            //    Summary = Summaries[rng.Next(Summaries.Length)]
            //})
            //.ToArray();
        }
        //[ResponseType(typeof(PersonalDetail))]
        //public IHttpActionResult GetPersonalDetail(int id)
        //{
        //    PersonalDetail personalDetail = db.PersonalDetails.Find(id);
        //    if (personalDetail == null)
        //    {
        //        return NotFound();
        //    }

        //    return Ok(personalDetail);
        //}


        [HttpPost]
        public void StartService()
        {
            //ZMQClient();

            ManagedStrangleDelta manageDelta = new ManagedStrangleDelta();
            //ZMQClient zMQClient = new ZMQClient();
            //zMQClient.ZMQSubcribeAllTicks(manageDelta);
        }
        //public void ZMQClient()
        //{
        //    ManagedStrangleDelta manageDelta = new ManagedStrangleDelta();
        //    //using (var context = new ZContext())
        //    //using (var subscriber = new ZSocket(context, ZSocketType.SUB))
        //    //{
        //    //    subscriber.Connect("tcp://127.0.0.1:5555");
        //    //    subscriber.Subscribe("");
        //    //    while (true)
        //    //    {
        //    //        using (ZMessage message = subscriber.ReceiveMessage())
        //    //        {
        //    //            // Read envelope with address
        //    //            byte[] tickData = message[0].Read();
        //    //            Tick[] ticks = TickDataSchema.ParseTicks(tickData);
        //    //            manageDelta.OnNext(ticks);
        //    //        }
        //    //    }
        //    //}
        //}

        //[HttpPost]
        //public async Task CreateOptionStrategy(OptionStrategy ostrategy)
        //{
        //    ///Depending on strategy, UI will call apppropriate API. This API will be called to place manual orders only.
        //    DataLogic dl = new DataLogic();
        //    ostrategy.Id = dl.CreateOptionStrategy(ostrategy);

        //    MarketOrders orders = new MarketOrders();
        //    orders.PlaceOrder(ostrategy.Id, ostrategy.Orders);

        //}

        private static readonly string[] Summaries = new[]
       {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        [HttpGet]
        public IEnumerable<WeatherForecast> Get()
        {
            var rng = new Random();
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = rng.Next(-20, 55),
                Summary = Summaries[rng.Next(Summaries.Length)]
            })
            .ToArray();
        }

        [HttpPost]
        public void ManageStrangleDelta(StrangleDetails strangle)
        {
            if(!ModelState.IsValid)
            {
                return;
            }
            ManagedStrangleDelta manageDelta = new ManagedStrangleDelta();
            manageDelta.ManageStrangleDelta(strangle.peToken, strangle.ceToken, strangle.peSymbol, strangle.ceSymbol, 
                Convert.ToDouble(strangle.pelowerThreshold), Convert.ToDouble(strangle.peUpperThreshold),
                Convert.ToDouble(strangle.celowerThreshold), Convert.ToDouble(strangle.ceUpperThreshold), 
                strangle.stopLossPoints, strangle.strangleId);
        }
        
    }
}