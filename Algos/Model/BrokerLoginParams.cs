using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Algos.Model
{
    public class BrokerLoginParams
    {
        [Key]
        public string ClientId { get; set; }
        public string? ClientName { get; set; }
        public string? Password { get; set; }
        public string? RequestToken { get; set; }
        public string? AccessToken { get; set; }
        public string? OTP { get; set; }
        public string? Action { get; set; }
        public string? Status { get; set; }

        public int BrokerId { get; set; }

        public string? BrokerName { get; set; }

        public bool? login { get; set; }
        public string? url { get; set; }


        public DateTime LoggedInDate { get; set; } = DateTime.UtcNow;


        // Foreign key to ApplicationUser
        public string? ApplicationUserId { get; set; }
    }
}
