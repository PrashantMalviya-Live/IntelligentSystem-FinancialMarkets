using Azure;
using StockMarketAlertsApp.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StockMarketAlertsApp.Clients
{
    public class AlertsClient(HttpClient httpClient)
    {

        public async Task<Int16> SetAlertAsync(AlertTrigger alertTrigger)
        {
            //var serializeOptions = new JsonSerializerOptions();
            //serializeOptions.Converters.Add(new IndicatorConverterWithTypeDiscriminator());


            HttpResponseMessage httpResponse = await httpClient.PostAsJsonAsync("alert", alertTrigger);

            ArgumentNullException.ThrowIfNull(httpResponse);

            if (httpResponse.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return 1;
            }
            else
            {
                return -1;
            }
        }
        public async Task<AlertTrigger[]> GetUserAlerts(string userId)
        {
            return await httpClient.GetFromJsonAsync<AlertTrigger[]>($"alert/user/{userId}")??[];
        }

        public async Task<AlertMessage[]> GetGeneratedAlerts(string userId)
        {
            return await httpClient.GetFromJsonAsync<AlertMessage[]>($"alert/generated/{userId}") ?? [];
        }
        

        public async Task<AlertTrigger[]> GetAlertsbyAlertId(string alertId)
        {
            return await httpClient.GetFromJsonAsync<AlertTrigger[]>($"alert/{alertId}") ?? [];
        }
        public async Task<decimal> GetUserCredits(string userId)
        {
            return await httpClient.GetFromJsonAsync<decimal>($"alert/credit/{userId}");
        }
        public async Task<decimal> AddUserCredits(string userId, decimal credits)
        {
            HttpResponseMessage response = await httpClient.PutAsJsonAsync<decimal>($"alert/credit/{userId}", credits);
            return await response.Content.ReadFromJsonAsync<decimal>();
        }

    }

    //public class IndicatorConverterWithTypeDiscriminator : JsonConverter<IIndicator>
    //{
    //    enum TypeDiscriminator
    //    {
    //        EMA = 1,
    //        SMA = 2
    //    }

    //    public override bool CanConvert(Type typeToConvert) =>
    //        typeof(IIndicator).IsAssignableFrom(typeToConvert);


    //    public override void Write(
    //        Utf8JsonWriter writer, IIndicator person, JsonSerializerOptions options)
    //    {
    //        writer.WriteStartObject();

    //        if (person is ExponentialMovingAverage customer)
    //        {
    //            writer.WriteNumber("LHSIndicator", (int)TypeDiscriminator.EMA);
    //            //writer.WriteNumber("CreditLimit", customer.CreditLimit);
    //        }
    //        else if (person is SimpleMovingAverage employee)
    //        {
    //            writer.WriteNumber("LHSIndicator", (int)TypeDiscriminator.SMA);
    //            //writer.WriteString("OfficeNumber", employee.OfficeNumber);
    //        }

    //        //writer.WriteString("Name", person.Name);

    //        writer.WriteEndObject();
    //    }
    //}
}
