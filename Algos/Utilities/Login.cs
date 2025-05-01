using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KiteConnect;
using GlobalLayer;
using BrokerConnectWrapper;
using KiteConnect;
using DBAccess;
namespace Algorithms.Utilities
{
    public class Login
    {
        private readonly IRDSDAO _rdsDAO;
        public Login(IRDSDAO rdsDAO)
        {
            _rdsDAO = rdsDAO;
        }
        public bool UpdateUser(User activeUser)
        {
            DataLogic dl = new DataLogic(_rdsDAO);
            return dl.UpdateUser(activeUser);
        }
        public User GetActiveUser(int brokerid, string userid="")
        {
            DataLogic dl = new DataLogic(_rdsDAO);
            return dl.GetActiveUser(brokerid, userid);
        }
        public AspNetUser GetActiveApplicationUser(string userid)
        {
            DataLogic dl = new DataLogic(_rdsDAO);
            return dl.GetActiveApplicationUser(userid);
        }

        private static void OnTokenExpire()
        {
            Logger.LogWrite("Need to login again");
        }
    }
}
