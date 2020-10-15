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
using GrpcLoggerService;

namespace GrpcPeerLoggerService
{
    public class PeerOrderService : ServiceOrderAlerter.ServiceOrderAlerterBase
    {
        private GrpcLoggerService.OrderMessage MessageConverter(OrderMessage orderMessage)
        {
            return new GrpcLoggerService.OrderMessage
            {
                Orderid = orderMessage.Orderid,
                Algorithm = orderMessage.Algorithm,
                AlgoInstance = orderMessage.AlgoInstance,
                InstrumentToken = orderMessage.InstrumentToken,
                OrderTime = orderMessage.OrderTime,
                OrderType = orderMessage.OrderType,
                Price = orderMessage.Price,
                Quantity = orderMessage.Quantity,
                Status = orderMessage.Status,
                StatusMessage = orderMessage.StatusMessage,
                TradingSymbol = orderMessage.TradingSymbol,
                TransactionType = orderMessage.TransactionType,
                TriggerPrice = orderMessage.TriggerPrice
            };
        }

        public override Task<PublishStatus> Publish(OrderMessage request, ServerCallContext context)
        {
            try
            {
                OrderService.CurrentOrderMessage = MessageConverter(request);
                ClientOrderRepository.AddOrder(request);
                OrderService.manualReset.Set();
                return Task.FromResult(new PublishStatus { Status = true });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new PublishStatus { Status = false });
            }
        }
    }
}
