using GlobalLayer;
using LiveMarketCockpit.ViewModel;
using ZMQFacade;

namespace LiveMarketCockpit;

public partial class MainPage : ContentPage
{
	int count = 0;
    OptionsChain pc = null;


    public MainPage()
	{
		InitializeComponent();
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
        Task task = Task.Run(() => NMQClientSubscription(pc, 260105));
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

