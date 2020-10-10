using Algorithms.Utilities;
using Algos.TLogics;
using GlobalLayer;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Data;
using ZMQFacade;

namespace ManageStrangleValueService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StrangleValueController : ControllerBase
    {
        [HttpGet]
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
            ManageStrangleValue manageValue = new ManageStrangleValue();
            ZMQClient zmqClient = new ZMQClient();
            zmqClient.Subscribe(manageValue);

        }
        //public void ZMQClient()
        //{
        //    ManageStrangleValue manageValue = new ManageStrangleValue();
        //    using (var context = new ZContext())
        //    using (var subscriber = new ZSocket(context, ZSocketType.SUB))
        //    {
        //        subscriber.Connect("tcp://127.0.0.1:5555");
        //        subscriber.Subscribe("");
        //        while (true)
        //        {
        //            using (ZMessage message = subscriber.ReceiveMessage())
        //            {
        //                // Read envelope with address
        //                byte[] tickData = message[0].Read();
        //                Tick[] ticks = TickDataSchema.ParseTicks(tickData);
        //                manageValue.OnNext(ticks);
        //            }
        //        }
        //    }
        //}




        [HttpPost]
        public void ManageStrangleValue(StrangleDetails strangle)
        {
            if (!ModelState.IsValid)
            {
                return;
            }
            ManageStrangleValue manageValue = new ManageStrangleValue();

            if (strangle.ThresholdinPercent)
            {
                manageValue.ManageStrangle(strangle.peToken, strangle.ceToken, strangle.peSymbol, strangle.ceSymbol,
                    stopLossPoints: strangle.stopLossPoints, strangleId: strangle.strangleId, 
                    peMaxLossPercent: strangle.peUpperThreshold, peMaxProfitPercent: strangle.pelowerThreshold,
                    ceMaxLossPercent: strangle.ceUpperThreshold, ceMaxProfitPercent: strangle.celowerThreshold);
            }
            else
            {
                manageValue.ManageStrangle(strangle.peToken, strangle.ceToken, strangle.peSymbol, strangle.ceSymbol,
                     stopLossPoints: strangle.stopLossPoints, strangleId: strangle.strangleId,
                     peMaxLossPoints: strangle.peUpperThreshold, peMaxProfitPoints: strangle.pelowerThreshold,
                     ceMaxLossPoints: strangle.ceUpperThreshold, ceMaxProfitPoints: strangle.celowerThreshold);
            }
        }
    }
}