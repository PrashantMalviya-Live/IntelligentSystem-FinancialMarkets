using System;
using System.Collections.Generic;
using System.Text;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;

namespace GlobalLayer
{
    public class AlertPublisher
    {
      

        public static void Publish(string title, string textMessage)
        {
           
            //var message = new Message()
            //{
            //    Data = new Dictionary<string, string>() { {"Nifty", "17600" }, },
            //    Token = registrationToken,
            //    Notification = new Notification() { Title = title, Body = textMessage }
            //};

            //string response = FirebaseMessaging.DefaultInstance.SendAsync(message).Result;
            //Console.WriteLine("Message Sent: " + response);
        }
    }
}
