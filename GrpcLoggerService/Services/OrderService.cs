using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Grpc.Core;
using System.Diagnostics.Eventing.Reader;
using GlobalLayer;
using Google.Protobuf.WellKnownTypes;
using System.Timers;
using GrpcPeerLoggerService;

namespace GrpcLoggerService
{
    public class OrderService : OrderAlerter.OrderAlerterBase, IOrderService
    {
        public static OrderMessage CurrentOrderMessage { get; set; }

        public static ManualResetEventSlim manualReset = new ManualResetEventSlim();
        private bool _firstTime;
        public OrderService()
        {
            _firstTime = true;
        }
        public override async Task Publish(IAsyncStreamReader<PublishStatus> requestStream, 
            IServerStreamWriter<OrderMessage> responseStream, ServerCallContext context)
        {
            while (true)
            {
                try
                {
                    if(_firstTime)
                    {
                        manualReset.Set();
                        _firstTime = false;
                        foreach (OrderMessage message in OrderRepository.Orders)
                        {
                           await responseStream.WriteAsync(message);
                        }
                        manualReset.Reset();
                    }
                    manualReset.Wait();
                    await responseStream.WriteAsync(CurrentOrderMessage);
                    OrderRepository.AddOrder(CurrentOrderMessage);
                    manualReset.Reset();
                }
                catch (Exception ex)
                {
                    break;
                }
            }
        }
    }
}
