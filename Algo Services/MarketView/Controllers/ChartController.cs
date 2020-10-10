using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MarketView.Hubs;
// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace TradeEMACross.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChartController : ControllerBase
    {
        private IHubContext<ChartHub> _hub;

        public ChartController(IHubContext<ChartHub> hub)
        {
            _hub = hub;
        }
        //public IActionResult Get()
        //{
        //    //var timerManager = new TimerManager(() => _hub.Clients.All.SendAsync("transferchartdata", DataManager.GetData()));
        //    //return Ok(new { Message = "Request Completed" });
        //}
    }
}
