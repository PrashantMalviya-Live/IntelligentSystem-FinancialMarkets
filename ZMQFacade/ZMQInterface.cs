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
        Task<bool> OnNext(Tick[] ticks);

        //Task<bool> OnNext(Tick tick);
    }
}
