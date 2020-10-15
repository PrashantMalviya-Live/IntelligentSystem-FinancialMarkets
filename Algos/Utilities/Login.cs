using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KiteConnect;
using GlobalLayer;
using ZConnectWrapper;
using KiteConnect;
namespace Algorithms.Utilities
{
    public class Login
    {
        public bool UpdateUser(User activeUser)
        {
            DataLogic dl = new DataLogic();
            return dl.UpdateUser(activeUser);
        }
        public User GetActiveUser()
        {
            DataLogic dl = new DataLogic();
            return dl.GetActiveUser();
        }
        
        private static void OnTokenExpire()
        {
            Logger.LogWrite("Need to login again");
        }
    }
}
