using Algos.TLogics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using ZMQFacade;
using Algorithms.Algorithms;
using Algorithms.Utilities;
using GlobalLayer;
using Global.Web;
using GlobalCore;
using System.Linq;
using System.Data;
using System.Threading.Tasks;
using Algorithms.Utilities.Views;
using Algos.Utilities.Views;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http;
using Algorithm.Algorithm;

namespace Alert.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AlertController : ControllerBase
    {
        [HttpGet]
        public void Get()
        {
         
        }
        // GET api/<HomeController>/5
        [HttpGet("user/{userid}")]
        public IEnumerable<AlertTriggerView> GetAlertTriggersForUser(string userid)
        {
            DataLogic dl = new DataLogic();
            List<AlertTriggerView> alertTriggers = dl.GetAlertTriggersForUser(userid);

            return alertTriggers;
        }
        [HttpGet("{alertid}")]
        public IEnumerable<AlertTriggerView> GetAlertsbyAlertId(string alertid)
        {
            DataLogic dl = new DataLogic();
            List<AlertTriggerView> alertTriggers = dl.GetAlertsbyAlertId(Int32.Parse(alertid));

            return alertTriggers;
        }
        [HttpGet("generated/{userid}")]
        public IEnumerable<GlobalLayer.Alert> GetGeneratedAlerts(string userid)
        {
            DataLogic dl = new DataLogic();
            List<GlobalLayer.Alert> alerts = dl.GetGeneratedAlerts(userid);

            return alerts;
        }

        [HttpGet("credit/{userid}")]
        public async Task<decimal> GetUserCreditsAsync(string userid)
        {
            DataLogic dl = new DataLogic();
            return await dl.GetUserCreditsAsync(userid);
        }


        [HttpPost]
        public async Task<int> UpdateAlertTriggerAsync([FromBody] AlertTriggerView alertTrigger)
        {
            DataLogic dataLogic = new DataLogic();
            int alertTriggerId = await dataLogic.UpdateAlertTriggerAsync(alertTrigger);

            return alertTriggerId;
        }
     
    }
}