using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using StockMarketAlertsApp.Models;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;

namespace StockMarketAlertsApp.Data
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        [Precision(18, 2)]
        public decimal Credit { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime? LastCreditDate { get; set; }

        public List<BrokerLoginParams> BrokerLoginParams { get; set; } = new List<BrokerLoginParams>();

    }



}
