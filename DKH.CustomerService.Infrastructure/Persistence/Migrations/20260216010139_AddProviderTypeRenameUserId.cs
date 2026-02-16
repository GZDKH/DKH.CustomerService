using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DKH.CustomerService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderTypeRenameUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TelegramUserId",
                table: "customer_profiles",
                newName: "user_id");

            migrationBuilder.RenameIndex(
                name: "IX_customer_profiles_StorefrontId_TelegramUserId",
                table: "customer_profiles",
                newName: "IX_customer_profiles_StorefrontId_user_id");

            migrationBuilder.AddColumn<string>(
                name: "provider_type",
                table: "customer_profiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Telegram");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "provider_type",
                table: "customer_profiles");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "customer_profiles",
                newName: "TelegramUserId");

            migrationBuilder.RenameIndex(
                name: "IX_customer_profiles_StorefrontId_user_id",
                table: "customer_profiles",
                newName: "IX_customer_profiles_StorefrontId_TelegramUserId");
        }
    }
}
