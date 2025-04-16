using System;
namespace Algorithm.Algorithm
{
    ///<summary>
    /// All generated Alerts
    /// </summary>
    public class UserAlert
    {
        public int ID { get; set; }
        public User2 User { get; set; }
        public DateTime TrigerredDate { get; set; }
        
        //Whether User was updated successfully about the alert
        //The reason for not alerting could be low cash balance, user inactive, or technical issue.
        public bool UserUpdated { get; set; }


    }
}
