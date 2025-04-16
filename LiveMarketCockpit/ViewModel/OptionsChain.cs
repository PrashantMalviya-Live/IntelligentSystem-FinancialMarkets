using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZMQFacade;
using GlobalLayer;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LiveMarketCockpit.ViewModel
{
    public partial class OptionsChain : ObservableObject, IZMQ
    {
        [ObservableProperty]
        string text;

        static OptionsChain()
        {
            OptionsList.Append("1");
        }
        ZMQClient zmqClient;
        
       







        public static IEnumerable<string> OptionsList { get; private set; }
        public void GetOptionList()
        {
            OptionsList = null;
            OptionsList = new string[] { };
        }
        private void RegisterWithBus()
        {
            Task task = Task.Run(() => NMQClientSubscription(this, 260105));
        }

        private async Task NMQClientSubscription(OptionsChain oc, uint token)
        {
            zmqClient = new ZMQClient();
            zmqClient.AddSubscriber(new List<uint>() { token });

            await zmqClient.Subscribe(oc);
        }

        public virtual void OnNext(Tick tick)
        {
            OptionsList.Append("1");
        }
    }
}
