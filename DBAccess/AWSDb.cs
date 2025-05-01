using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Newtonsoft.Json;
using Amazon.TimestreamWrite;
using Amazon.TimestreamQuery;
using GlobalLayer;
namespace DataAccess
{
    public class AWSDb
    {
        public static string GetAuroraRDSConnectionString()
        {
            string secretName = "rds-db-credentials/cluster-OBYPK32PB2P2HI4MDTOMTOH6YM/zm/1745667216012";
            string region = "ap-south-1";

            IAmazonSecretsManager client = new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(region));

            GetSecretValueRequest request = new GetSecretValueRequest
            {
                SecretId = secretName,
                VersionStage = "AWSCURRENT", // VersionStage defaults to AWSCURRENT if unspecified.
            };

            GetSecretValueResponse response;

            try
            {
                response = client.GetSecretValueAsync(request).Result;
                Logger.LogWrite(response.ToString());
            }
            catch (Exception e)
            {
                // For a list of the exceptions thrown, see
                // https://docs.aws.amazon.com/secretsmanager/latest/apireference/API_GetSecretValue.html
                throw e;
            }

            //string secret = response.SecretString;

            if (response.SecretString != null)
            {
                var secret = JsonConvert.DeserializeObject<Dictionary<string, string>>(response.SecretString);
                string host = secret["host"];
                string port = secret["port"];
                string database = secret["database"];
                string user = secret["username"];
                string password = secret["password"];

                return $"Server={host};Port=3306;Database={database};User ID={user};Password={password};SslMode=Preferred";
            }
            else
            {
                throw new Exception("Unable to retrieve the secret.");
            }
        }
        public static string GetAWSTimestreamConnectionString()
        {
            string secretName = "rds-db-credentials/cluster-OBYPK32PB2P2HI4MDTOMTOH6YM/zm/1745667216012";
            string region = "ap-south-1";

            IAmazonSecretsManager client = new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(region));

            GetSecretValueRequest request = new GetSecretValueRequest
            {
                SecretId = secretName,
                VersionStage = "AWSCURRENT", // VersionStage defaults to AWSCURRENT if unspecified.
            };

            GetSecretValueResponse response;

            try
            {
                response = client.GetSecretValueAsync(request).Result;
            }
            catch (Exception e)
            {
                // For a list of the exceptions thrown, see
                // https://docs.aws.amazon.com/secretsmanager/latest/apireference/API_GetSecretValue.html
                throw e;
            }

            //string secret = response.SecretString;

            if (response.SecretString != null)
            {
                var secret = JsonConvert.DeserializeObject<Dictionary<string, string>>(response.SecretString);
                string host = secret["host"];
                string database = secret["database"];
                string user = secret["username"];
                string password = secret["password"];

                return $"Server={host};Database={database};User={user};Password={password};";
            }
            else
            {
                throw new Exception("Unable to retrieve the secret.");
            }
        }
    }
}


