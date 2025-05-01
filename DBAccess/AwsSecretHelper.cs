using Amazon.Runtime;
using Amazon.SecretsManager.Model;
using Amazon.SecretsManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

namespace DataAccess
{
    public class AwsSecretHelper
    {
        private readonly IAmazonSecretsManager _secretsManager;

        public AwsSecretHelper(IAmazonSecretsManager secretsManager)
        {
            _secretsManager = secretsManager;
        }

        public async Task<DbSecret> GetDbSecretAsync(string secretName)
        {
            var request = new GetSecretValueRequest { SecretId = secretName };
            var response = await _secretsManager.GetSecretValueAsync(request);

            var secretJson = response.SecretString;
            return JsonSerializer.Deserialize<DbSecret>(secretJson);
        }
    }

    public class DbSecret
    {
        public string host { get; set; }
        public string username { get; set; }
        public string password { get; set; }
        public string database { get; set; }
    }

}
