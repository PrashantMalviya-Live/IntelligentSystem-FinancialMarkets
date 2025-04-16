using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using StockMarketAlerts.Model;


namespace StockMarketAlerts.DBContext
{
    public class StockAlertContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<UserAlertSelector> UserAlertSelectors { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(Utility.GetConnectionString());
            base.OnConfiguring(optionsBuilder);
        }

    }
}
