using System.Configuration;
using Microsoft.Extensions.Configuration;
namespace DataAccess
{
    public class Utility
    {
        public static string GetConnectionString()
        {
            string connectionstring = "Data Source=.;Initial Catalog=NSEData;User Id=zm;Password=zm;Persist Security Info=True";
            return connectionstring;
        }
        public static string GetMemSqlConnectionString()
        {
            string connectionstring = "server = localhost; user = zm; password = zm; database = NSEData";
            return connectionstring;
        }

        public static string GetConnectionString(IConfiguration iConfig)
        {
            string connectionString = iConfig.GetSection("MySettings").GetSection("DbConnection").Value;
            return connectionString;
        }
    }
}
