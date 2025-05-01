using Algorithm.Algorithm;
using Microsoft.AspNetCore.Mvc;
using KotakConnect;
using GlobalLayer;
using Algorithms.Utilities;
using BrokerConnectWrapper;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Identity;
using DBAccess;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace StockAlerts.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PositionsController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private const string okey = "ACTIVE_USER_POSITIONS";
        private const string userKey = "ACTIVE_USER";

        private IMemoryCache _cache;
        private readonly IRDSDAO _rdsDAO;

        public PositionsController(IMemoryCache cache, IHttpClientFactory httpClientFactory, IRDSDAO rdsDAO)
        {
            this._cache = cache;
            this._httpClientFactory = httpClientFactory;
            _rdsDAO = rdsDAO;
        }


        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET position by client id
        [HttpGet("{id}/{brokerid}")]
        public async Task<List<KotakPosition>> Get(string id, int brokerid)
        {
            //First get user claims. This will show which all accounts he has login, then try to get positions from all, and append.
            //var loginClaims = User.Claims.Where(x => x.Equals(id));

            //Login l = new Login();
            //AspNetUser applicationUser = l.GetActiveApplicationUser(id);
            brokerid = 1;
            KoConnect koConnect = new KoConnect(_rdsDAO);
            GlobalLayer.User activeUser = koConnect.GetUserByApplicationUserId(userId: id, brokerId: brokerid);

            var httpClient = KoConnect.ConfigureHttpClient2(activeUser, _httpClientFactory.CreateClient());

            Kotak kotak = new Kotak();
            //http client is needed
            var positions = kotak.GetPositions(httpClient, activeUser);
            

            //foreach (var claim in loginClaims)
            //{
            //    if(claim.Value == "Kotak")
            //    {
            //        //set kotak acess token
            //        Kotak kotak = new Kotak();
            //        PositionResponse pr = kotak.GetPositions();
            //        
            //    }
            //}
            return positions;

        }

        // POST api/<PositionsController>
        [HttpPost]
        public void Post([FromBody] string value)
        {
        }

        // PUT api/<PositionsController>/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/<PositionsController>/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
