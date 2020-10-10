using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GlobalLayer;
using Microsoft.AspNetCore.SignalR;

namespace MarketView.Hubs
{
    public class LogHub : Hub
    {
        public async Task SendMessage(LogData log)
        {
            await Clients.All.SendAsync("LogUpdate", log);
        }
    }
}
