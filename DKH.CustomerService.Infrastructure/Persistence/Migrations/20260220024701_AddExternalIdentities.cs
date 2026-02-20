using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DKH.CustomerService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalIdentities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customer_external_identities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    provider_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    linked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_customer_external_identities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_external_identities_customer_profiles_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_customer_external_identities_customer_id",
                table: "customer_external_identities",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_customer_external_identities_provider_email",
                table: "customer_external_identities",
                columns: new[] { "provider", "email" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_external_identities_provider_provider_user_id",
                table: "customer_external_identities",
                columns: new[] { "provider", "provider_user_id" },
                unique: true);

            // Seed external identities from existing customer profiles
            migrationBuilder.Sql("""
                INSERT INTO customer_external_identities (
                    "Id", customer_id, provider, provider_user_id, email,
                    display_name, is_primary, linked_at,
                    "CreationTime", "CreatorId",
                    "LastModificationTime", "LastModifierId",
                    "IsDeleted", "DeleterId", "DeletionTime"
                )
                SELECT
                    gen_random_uuid(),
                    "Id",
                    provider_type,
                    user_id,
                    "Email",
                    NULL,
                    true,
                    "CreationTime",
                    "CreationTime",
                    "CreatorId",
                    "LastModificationTime",
                    "LastModifierId",
                    "IsDeleted",
                    "DeleterId",
                    "DeletionTime"
                FROM customer_profiles
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_external_identities");
        }
    }
}
