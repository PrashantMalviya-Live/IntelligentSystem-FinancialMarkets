using System;
using System.Collections.Generic;
using System.Text;
//using Twilio;
//using Twilio.Rest.Api.V2010.Account;
//using Twilio.Types;

namespace Algos.Utilities
{
    public class TwilioWhatsApp
    {
        public static void SendWhatsAppMessage(string text)
        {
            var accountSid = "AC7b4f5f8cb0b56bb9906240b860299fd4";
            var authToken = "43809a842cd9540573e4e46824ac5792";
            TwilioClient.Init(accountSid, authToken);

            var messageOptions = new CreateMessageOptions(
                new PhoneNumber("whatsapp:+919650084883"));
            messageOptions.From = new PhoneNumber("whatsapp:+14155238886");
            messageOptions.Body = text;

            var message = MessageResource.Create(messageOptions);
            Console.WriteLine(message.Body);
        }
    }
}
