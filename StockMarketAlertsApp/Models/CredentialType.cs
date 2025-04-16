using Microsoft.Identity.Client.Platforms.Features.DesktopOs.Kerberos;

namespace StockMarketAlertsApp.Models
{
    public class CredentialType
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public int? Position { get; set; }

        public virtual ICollection<Credential> Credentials { get; set; }
    }
}
