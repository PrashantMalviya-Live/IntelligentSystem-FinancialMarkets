using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using StockMarketAlertsApp.Data;
using StockMarketAlertsApp.Models;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace StockMarketAlertsApp.Clients
{
    public class LoginClient(HttpClient httpClient)
    {
        public async Task<BrokerLoginParams> DataBrokerLogin(string requestToken)
        {
            //sucess here will set the policy to loggin user
            //var httpResponse = await httpClient.GetAsync($"login?request_token={requestToken}");

            var brokerLoginParams = await httpClient.GetFromJsonAsync<BrokerLoginParams>($"login?request_token={requestToken}");

            //object? result = null;

            //if (httpResponse.IsSuccessStatusCode)
            //{
            //    result = await httpClient.GetFromJsonAsync<object>("login") ?? null;
            //}
            //else
            //{

            //}

            //BrokerLoginParams brokerLoginParams;// = new BrokerLoginParams() { ClientId="" };
            //string message;

            //message = await httpResponse.Content.ReadAsStringAsync();
            //brokerLoginParams = JsonSerializer.Deserialize<BrokerLoginParams>(message);

            //if (httpResponse.StatusCode == System.Net.HttpStatusCode.OK)
            //{
            //    message = await httpResponse.Content.ReadAsStringAsync();

            //    brokerLoginParams = JsonSerializer.Deserialize<BrokerLoginParams>(message);
            //}
            //else if (httpResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            //{
            //    message = await httpResponse.Content.ReadAsStringAsync();

            //    var zerodhaLoginResponse = JsonSerializer.Deserialize<ZerodhaLoginResponse>(message);
            //    message = zerodhaLoginResponse.url;
            //}
            //else
            //{
            //    throw new Exception("Login Not Successful");
            //}

            return brokerLoginParams;

        }
        public async Task<bool> ZerodhaLoadTokens()
        {
            //sucess here will set the policy to loggin user
            //var httpResponse = await httpClient.GetAsync($"login?request_token={requestToken}");

            return await httpClient.GetFromJsonAsync<bool>($"home");

        }


        public async Task<int> BrokerLogin(string brokerName, BrokerLoginParams brokerLoginParams)
        {
            //var data = $"{{\"userid\": \"{brokerLoginParams.ClientId}\", " +
            //    $"\"pwd\": \"{brokerLoginParams.Password}\"," +
            //    $"\"accessToken\": \"{brokerLoginParams.AccessToken}\"," +
            //    $"\"otp\": \"{brokerLoginParams.OTP}\"" +
            //    $"\"applicationUserId\": \"{brokerLoginParams.ApplicationUserId}\"" +
            //    $"}}";

            var kotakLoginParams = new KotakLoginParam()
            {
                accessToken = brokerLoginParams.AccessToken,
                userid = brokerLoginParams.ClientId,
                pwd = brokerLoginParams.Password,
                otp = brokerLoginParams.OTP,
                applicationUserId = brokerLoginParams.ApplicationUserId,
            };
           

            HttpResponseMessage httpResponse = await httpClient.PostAsJsonAsync("kotaklogin", kotakLoginParams);

            string result = await httpResponse.Content.ReadAsStringAsync();
            
            var kotakresponse = JsonSerializer.Deserialize<KotakLoginResponse>(result);

            if (httpResponse.StatusCode == System.Net.HttpStatusCode.OK)
            {
                //loggedin

                
             
            }

            else if (kotakresponse.statusCode == 418)
            {
                //show OTP
            }
            return kotakresponse.statusCode;
            //return httpResponse.Content.ToString();

            //this.http.post<any>(this._baseUrl + 'api/kotaklogin', data).subscribe(result => {

            //    if (result.message == "200 OK")
            //    {

            //        this.router.navigateByUrl('/trade');
            //        //window.location.href = baseUrl;
            //        //this.submitEM.emit(this.loginForm.value);
            //        //return result.userName;
            //    }
            //    else if
            //      (result.message == "418 TP")
            //    {

            //        this.showotp = true;
            //        //window.location.href = baseUrl;
            //        //this.submitEM.emit(this.loginForm.value);
            //        //return result.userName;
            //    }


            //}, error => console.error(error));


            //clientId = "Client";
            //string password = "password";
            //string accessToken = "accessToken";



            ////sucess here will set the policy to loggein user
            //httpClient.PostAsJsonAsync("KotakLogin", clientId);
        }

        
    }
}