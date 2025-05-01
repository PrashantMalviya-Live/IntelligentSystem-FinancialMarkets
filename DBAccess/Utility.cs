using System.Configuration;
using Microsoft.Extensions.Configuration;
namespace DataAccess
{
    public class Utility
    {
        public static string GetConnectionString()
        {
            //return GetAWSConnectionString();
            string connectionstring = "Data Source=.;Initial Catalog=NSEData;User Id=zm;Password=zm;Persist Security Info=True";
            return connectionstring;
        }

        public static string GetAuroraRDSConnectionString()
        {
            string connectionstring = AWSDb.GetAuroraRDSConnectionString(); // "Server=nseapplicationdata.cluster-ctoeck4omy50.ap-south-1.rds.amazonaws.com;Database=NSEData;User=zm;Password=mypassword;";
            return connectionstring;
        }
        public static string GetTimeStreamConnectionString()
        {
            string connectionstring = AWSDb.GetAWSTimestreamConnectionString(); // "Server=nseapplicationdata.cluster-ctoeck4omy50.ap-south-1.rds.amazonaws.com;Database=NSEData;User=zm;Password=mypassword;";
            return connectionstring;
        }
        //public static string GetAWSConnectionString()
        //{
        //    string connectionstring = AWSDb.GetConnectionString(); // "Server=nseapplicationdata.cluster-ctoeck4omy50.ap-south-1.rds.amazonaws.com;Database=NSEData;User=zm;Password=mypassword;";
        //    return connectionstring;
        //}

        //public static string GetMemSqlConnectionString()
        //{
        //    string connectionstring = "server = localhost; user = z; password = m; database = NSEData";
        //    return connectionstring;
        //}

        public static string GetConnectionString(IConfiguration iConfig)
        {
            string connectionString = iConfig.GetSection("MySettings").GetSection("DbConnection").Value;
            return connectionString;
        }
    }
}
