using Algorithms.Utilities;
using GlobalLayer;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
namespace MarketView.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OptionChainController : ControllerBase
    {
        public const int CE = 0;
        public const int PE = 1;


        // GET: api/OptionChainsEF
        [HttpGet]
        public async Task<string>  GetBaseInstruments()
        {
            //Retrive all options
            List<Instrument> bInstruments = await RetrieveBaseInstruments();

            return JsonSerializer.Serialize(bInstruments);
        }
        public async Task<List<Instrument>> RetrieveBaseInstruments()
        {
            DataLogic dl = new DataLogic();
            return dl.RetrieveBaseInstruments();
        }


        //[HttpGet("{token}")]
        //public async Task<List<string>> GetOptionExpires(uint token)
        //{
        //    DataLogic dl = new DataLogic();

        //    //Retrieve expiry list ONLY here and fulllist at expiry click
        //    List<string> expiryList = await GetOptionExpires(token);
        //    return expiryList;
        //}

        //public async Task<List<string>> GetOptionExpires()
        //{
        //    DataLogic dl = new DataLogic();
        //    return dl.GetOptionExpires();
        //}

        [HttpGet("{btoken,expiry}")]
        public async Task<string> GetOptions(UInt32 btoken, DateTime expiry)
        {
            //Retrieve expiry list ONLY here and fulllist at expiry click
            List<OptionChain> optionsList =  await RetrieveOptions(btoken, expiry);
            
            return JsonSerializer.Serialize(optionsList);
        }

        //[HttpGet("{token}")]
        //public async Task<Option> GetTick(UInt32 token)
        //{
        //    //Retrieve expiry list ONLY here and fulllist at expiry click
        //    Tick tick = ZMQClient.ZMQSubcribebyToken(new string[] { string.Format("{0}", token) });

        //    return new Option() { LastPrice = tick.LastPrice, InstrumentToken = token };
        //}
        
        public async Task<List<OptionChain>> RetrieveOptions(UInt32 btoken, DateTime expiry)
        {
            DataLogic dl = new DataLogic();

            List<Instrument> optionsList = dl.RetrieveOptions(btoken, expiry);

            var sortedOptionList = optionsList.GroupBy(x => x.Strike).OrderBy(x => x.Key);

            List<OptionChain> optionsChain = new List<OptionChain>();
            foreach (IGrouping<decimal, Instrument> instruments in sortedOptionList)
            {
                decimal strike = instruments.Key;



                Option[] options = new Option[2];
                foreach (Instrument instrument in instruments)
                {
                    options[instrument.InstrumentType == "CE" ? CE : PE] = new Option() { Delta = 0, DeltaOI = 0, 
                        InstrumentToken = instrument.InstrumentToken, IV = 0, LastPrice = 0, OI = 0 };
                }

                //Tick[] ticks = ZMQClient.ZMQSubcribeAllTicks();

                //List<Option> optionList = new List<Option>();
                //foreach (Tick tick in ticks)
                //{
                //    optionList.Add(new Option() { LTP = tick.LastPrice, InstrumentToken = token });
                //}

                OptionChain optionChain = new OptionChain()
                {
                    BToken = btoken,
                    Expiry = expiry,
                    Strike = strike,
                    Option = options
                };

                optionsChain.Add(optionChain);
            }
            return optionsChain;
        }

        //[HttpPost]
        //public async Task CreateOptionStrategy(OptionStrategy ostrategy)
        //{
        //    ///Depending on strategy, UI will call apppropriate API. This API will be called to place manual orders only.
        //    DataLogic dl = new DataLogic();
        //    ostrategy.Id = dl.CreateOptionStrategy(ostrategy);

        //    MarketOrders orders = new MarketOrders();
        //    orders.PlaceOrder(ostrategy.Id, ostrategy.Orders);
           
        //}
    }
}