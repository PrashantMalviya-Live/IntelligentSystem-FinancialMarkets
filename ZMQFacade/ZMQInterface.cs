using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GlobalLayer;
namespace ZMQFacade
{
    public interface IZMQ
    {
        ///Task<bool> OnNext(Tick[] ticks);

        //Task<bool> OnNext(Tick tick);

        void OnNext(Tick tick);

        void StopTrade(bool st);
    }
    public interface ICZMQ
    {
        void OnNext(string channel, string data);

        void StopTrade(bool st);
    }
}
