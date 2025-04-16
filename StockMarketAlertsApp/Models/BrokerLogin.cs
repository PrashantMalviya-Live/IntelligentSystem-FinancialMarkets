using StockMarketAlertsApp.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StockMarketAlertsApp.Models
{
    public class BrokerLoginParams
    {
        [Key]
        public required string ClientId { get; set; }
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


        [Column(TypeName = "datetime")]
        public DateTime LoggedInDate { get; set; } = DateTime.UtcNow;


        // Foreign key to ApplicationUser
        public string ApplicationUserId { get; set; }
        public ApplicationUser ApplicationUser { get; set; }
    }
}
