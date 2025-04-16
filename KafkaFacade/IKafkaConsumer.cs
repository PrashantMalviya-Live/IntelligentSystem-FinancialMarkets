using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GlobalLayer;
namespace KafkaFacade
{
    public interface IKConsumer
    {
        void OnNext(Tick tick);

        //Task<bool> OnNext(Tick tick);
    }
}
