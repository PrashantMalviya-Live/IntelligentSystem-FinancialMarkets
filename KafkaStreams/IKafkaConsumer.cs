using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Global;
namespace KafkaStreams
{
    public interface IKafkaConsumer
    {
        Task<bool> OnNext(Tick[] ticks);

        //Task<bool> OnNext(Tick tick);
    }
}
