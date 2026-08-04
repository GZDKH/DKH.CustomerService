using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DKH.CustomerService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGlobalCustomerAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "account_reconciliation_attempt_count",
                table: "customer_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "account_reconciliation_reason_code",
                table: "customer_profiles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "account_reconciliation_status",
                table: "customer_profiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "PendingProof");

            migrationBuilder.AddColumn<Guid>(
                name: "customer_account_id",
                table: "customer_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_account_reconciliation_attempt_at",
                table: "customer_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "customer_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    identity_issuer = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    identity_subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    verified_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    email_verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    preferred_locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Active"),
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
                    table.PrimaryKey("PK_customer_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "linked_customer_identities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_authority = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    provider_subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    provider_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    linked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    legacy_external_identity_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_linked_customer_identities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_linked_customer_identities_customer_accounts_customer_accou~",
                        column: x => x.customer_account_id,
                        principalTable: "customer_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "storefront_memberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storefront_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legacy_customer_profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    first_authenticated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_authenticated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_activity_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Active"),
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
                    table.PrimaryKey("PK_storefront_memberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_storefront_memberships_customer_accounts_customer_account_id",
                        column: x => x.customer_account_id,
                        principalTable: "customer_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_storefront_memberships_customer_profiles_legacy_customer_pr~",
                        column: x => x.legacy_customer_profile_id,
                        principalTable: "customer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_customer_profiles_account_reconciliation_status_last_accoun~",
                table: "customer_profiles",
                columns: new[] { "account_reconciliation_status", "last_account_reconciliation_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_profiles_customer_account_id",
                table: "customer_profiles",
                column: "customer_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_accounts_verified_email",
                table: "customer_accounts",
                column: "verified_email");

            migrationBuilder.CreateIndex(
                name: "ux_customer_accounts_issuer_subject",
                table: "customer_accounts",
                columns: new[] { "identity_issuer", "identity_subject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_linked_customer_identities_account",
                table: "linked_customer_identities",
                column: "customer_account_id");

            migrationBuilder.CreateIndex(
                name: "ux_linked_customer_identities_authority_subject",
                table: "linked_customer_identities",
                columns: new[] { "provider_authority", "provider_subject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_linked_customer_identities_legacy",
                table: "linked_customer_identities",
                column: "legacy_external_identity_id",
                unique: true,
                filter: "\"legacy_external_identity_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_storefront_memberships_storefront",
                table: "storefront_memberships",
                column: "storefront_id");

            migrationBuilder.CreateIndex(
                name: "ux_storefront_memberships_account_storefront",
                table: "storefront_memberships",
                columns: new[] { "customer_account_id", "storefront_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_storefront_memberships_legacy_profile",
                table: "storefront_memberships",
                column: "legacy_customer_profile_id",
                unique: true,
                filter: "\"legacy_customer_profile_id\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_customer_profiles_customer_accounts_customer_account_id",
                table: "customer_profiles",
                column: "customer_account_id",
                principalTable: "customer_accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_customer_profiles_customer_accounts_customer_account_id",
                table: "customer_profiles");

            migrationBuilder.DropTable(
                name: "linked_customer_identities");

            migrationBuilder.DropTable(
                name: "storefront_memberships");

            migrationBuilder.DropTable(
                name: "customer_accounts");

            migrationBuilder.DropIndex(
                name: "IX_customer_profiles_account_reconciliation_status_last_accoun~",
                table: "customer_profiles");

            migrationBuilder.DropIndex(
                name: "IX_customer_profiles_customer_account_id",
                table: "customer_profiles");

            migrationBuilder.DropColumn(
                name: "account_reconciliation_attempt_count",
                table: "customer_profiles");

            migrationBuilder.DropColumn(
                name: "account_reconciliation_reason_code",
                table: "customer_profiles");

            migrationBuilder.DropColumn(
                name: "account_reconciliation_status",
                table: "customer_profiles");

            migrationBuilder.DropColumn(
                name: "customer_account_id",
                table: "customer_profiles");

            migrationBuilder.DropColumn(
                name: "last_account_reconciliation_attempt_at",
                table: "customer_profiles");
        }
    }
}
