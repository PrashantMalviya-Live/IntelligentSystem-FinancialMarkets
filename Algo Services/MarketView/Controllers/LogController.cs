using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MarketView.Hubs;
using GlobalLayer;
using Global.Web;
// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace MarketView.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LogController : ControllerBase
    {
        private IHubContext<LogHub> _hub;
        
        public LogController(IHubContext<LogHub> hub)
        {
            _hub = hub;
        }
        public IActionResult Get()
        {
            var timerManager = new TimerManager(() => _hub.Clients.All.SendAsync("transferchartdata", DataManager.GetData()));
            return Ok(new { Message = "Request Completed" });
        }

        public bool PublishLog(LogData log)
        {

            return true;
        }
    }
}
