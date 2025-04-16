using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeltaExchangeConnect
{
    public interface IWebSocket
    {
        event OnConnectHandler OnConnect;
        event OnCloseHandler OnClose;
        event OnDataHandler OnData;
        event OnErrorHandler OnError;
        bool IsConnected();
        Task ConnectAsync(string Url);
        void Send(string Message);
        void Close(bool Abort = false);
        Task Subscribe(Dictionary<string, List<string>> channelsAndSymbols);//string channel, List<string> symbols);
        Task UnSubscribe(Dictionary<string, List<string>> channelsAndSymbols);//string channel, List<string> symbols);
    }
}
