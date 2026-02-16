using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DKH.CustomerService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _202602161200_AddIsPremium : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_premium",
                table: "customer_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_premium",
                table: "customer_profiles");
        }
    }
}
