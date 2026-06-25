using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DKH.CustomerService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAddressNameCompanyProvince : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Company",
                table: "customer_addresses",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "customer_addresses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "customer_addresses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Province",
                table: "customer_addresses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Company",
                table: "customer_addresses");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "customer_addresses");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "customer_addresses");

            migrationBuilder.DropColumn(
                name: "Province",
                table: "customer_addresses");
        }
    }
}
