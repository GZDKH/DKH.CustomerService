using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DKH.CustomerService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customer_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StorefrontId = table.Column<Guid>(type: "uuid", nullable: false),
                    TelegramUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PhotoUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LanguageCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    account_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    blocked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    block_reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    blocked_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    suspended_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_activity_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    total_orders_count = table.Column<int>(type: "integer", nullable: false),
                    total_spent = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    email_verified = table.Column<bool>(type: "boolean", nullable: false),
                    email_verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    phone_verified = table.Column<bool>(type: "boolean", nullable: false),
                    phone_verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    email_notifications_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    telegram_notifications_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    sms_notifications_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    order_status_updates = table.Column<bool>(type: "boolean", nullable: false),
                    promotional_offers = table.Column<bool>(type: "boolean", nullable: false),
                    preferred_language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    preferred_currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "customer_addresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Street = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Building = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Apartment = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_addresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_addresses_customer_profiles_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wishlist_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductSkuId = table.Column<Guid>(type: "uuid", nullable: true),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Note = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wishlist_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wishlist_items_customer_profiles_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_customer_addresses_CustomerId",
                table: "customer_addresses",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_addresses_CustomerId_IsDefault",
                table: "customer_addresses",
                columns: new[] { "CustomerId", "IsDefault" },
                filter: "\"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_customer_profiles_Email",
                table: "customer_profiles",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_customer_profiles_Phone",
                table: "customer_profiles",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_customer_profiles_StorefrontId_TelegramUserId",
                table: "customer_profiles",
                columns: new[] { "StorefrontId", "TelegramUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wishlist_items_CustomerId",
                table: "wishlist_items",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_wishlist_items_CustomerId_ProductId_ProductSkuId",
                table: "wishlist_items",
                columns: new[] { "CustomerId", "ProductId", "ProductSkuId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_addresses");

            migrationBuilder.DropTable(
                name: "wishlist_items");

            migrationBuilder.DropTable(
                name: "customer_profiles");
        }
    }
}
