using System;
using System.Collections.Generic;

namespace GlobalLayer
{
    public class User2
    {
        public int ID { get; set; }
        public string UserName { get; set; }
        public DateTime EnrollmentDate { get; set; }

        public DateTime IsPremium { get; set; }

        public Decimal AccountBalance { get; set; }

        public bool Active { get; set; }

        public ICollection<UserPayment> UserPayments { get; set; }

        public ICollection<UserAlertSelector> UserAlertSelectors { get; set; }
    }
}
