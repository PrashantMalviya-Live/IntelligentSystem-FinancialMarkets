using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockMarketAlertsApp.Migrations
{
    /// <inheritdoc />
    public partial class brokerparamupdated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "login",
                table: "BrokerLoginParams",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "url",
                table: "BrokerLoginParams",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "login",
                table: "BrokerLoginParams");

            migrationBuilder.DropColumn(
                name: "url",
                table: "BrokerLoginParams");
        }
    }
}
