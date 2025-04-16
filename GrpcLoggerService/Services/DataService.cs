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
    public class DataService : DataManager.DataManagerBase, IDataService
    {
        public static DataMessage CurrentDataMessage { get; set; }

        public static ManualResetEventSlim manualReset = new ManualResetEventSlim();
        private bool _firstTime;
        public DataService()
        {
            _firstTime = true;
        }
        public override async Task Data(IAsyncStreamReader<DataStatus> requestStream,
            IServerStreamWriter<DataMessage> responseStream, ServerCallContext context)
        {
            while (true)
            {
                try
                {
                    if (_firstTime)
                    {
                        manualReset.Set();
                        _firstTime = false;
                        foreach (DataMessage message in DataRepository.Datas)
                        {
                            await responseStream.WriteAsync(message);
                        }
                        manualReset.Reset();
                    }
                    manualReset.Wait();
                    await responseStream.WriteAsync(CurrentDataMessage);
                    DataRepository.AddData(CurrentDataMessage);
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
