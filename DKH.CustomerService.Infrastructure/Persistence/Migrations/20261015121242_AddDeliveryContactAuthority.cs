using DKH.CustomerService.Infrastructure.DeliveryContacts;
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DKH.CustomerService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryContactAuthority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "delivery_contact_revisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorefrontId = table.Column<Guid>(type: "uuid", nullable: false),
                    LegacyProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddressId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_delivery_contact_revisions", x => x.Id);
                    table.CheckConstraint("ck_delivery_contact_revision", "\"Revision\" > 0 AND \"Digest\" ~ '^[0-9a-f]{64}$' AND \"SourceDigest\" ~ '^[0-9a-f]{64}$'");
                });

            migrationBuilder.CreateTable(
                name: "delivery_contact_consents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorefrontId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactRevision = table.Column<long>(type: "bigint", nullable: false),
                    ContactDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ScopeJson = table.Column<string>(type: "jsonb", nullable: false),
                    ScopeDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokeRequestDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RevokeOperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_delivery_contact_consents", x => x.Id);
                    table.CheckConstraint("ck_delivery_contact_consent", "\"Revision\" > 0 AND \"ValidUntilUtc\" > \"CreatedAtUtc\" AND \"ValidUntilUtc\" <= \"CreatedAtUtc\" + interval '5 minutes' AND ((\"RevokedAtUtc\" IS NULL AND \"RevokeOperationId\" IS NULL AND \"Revision\" = 1) OR (\"RevokedAtUtc\" IS NOT NULL AND \"RevokeOperationId\" IS NOT NULL AND \"Revision\" = 2))");
                    table.ForeignKey(
                        name: "FK_delivery_contact_consents_delivery_contact_revisions_Contac~",
                        column: x => x.ContactRevisionId,
                        principalTable: "delivery_contact_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_contact_leases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorefrontId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContactRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConsentRevision = table.Column<long>(type: "bigint", nullable: true),
                    Purpose = table.Column<int>(type: "integer", nullable: false),
                    ContextDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_delivery_contact_leases", x => x.Id);
                    table.CheckConstraint("ck_delivery_contact_lease", "\"ExpiresAtUtc\" > \"IssuedAtUtc\" AND \"ExpiresAtUtc\" <= \"IssuedAtUtc\" + interval '5 minutes' AND ((\"Purpose\" = 1 AND \"ConsentId\" IS NULL AND \"ConsentRevision\" IS NULL AND \"WorkspaceId\" IS NULL) OR (\"Purpose\" = 5 AND \"ConsentId\" IS NOT NULL AND \"ConsentRevision\" > 0 AND \"WorkspaceId\" IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_delivery_contact_leases_delivery_contact_consents_ConsentId",
                        column: x => x.ConsentId,
                        principalTable: "delivery_contact_consents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_delivery_contact_leases_delivery_contact_revisions_ContactR~",
                        column: x => x.ContactRevisionId,
                        principalTable: "delivery_contact_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_contact_consents_ContactRevisionId",
                table: "delivery_contact_consents",
                column: "ContactRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_contact_consents_OwnerAccountId_OperationId",
                table: "delivery_contact_consents",
                columns: new[] { "OwnerAccountId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_delivery_contact_leases_ActorAccountId_OperationId",
                table: "delivery_contact_leases",
                columns: new[] { "ActorAccountId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_delivery_contact_leases_ConsentId",
                table: "delivery_contact_leases",
                column: "ConsentId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_contact_leases_ContactRevisionId",
                table: "delivery_contact_leases",
                column: "ContactRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_contact_revisions_OwnerAccountId_OperationId",
                table: "delivery_contact_revisions",
                columns: new[] { "OwnerAccountId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_delivery_contact_revisions_OwnerAccountId_StorefrontId_Addr~",
                table: "delivery_contact_revisions",
                columns: new[] { "OwnerAccountId", "StorefrontId", "AddressId", "Revision" },
                unique: true);
            migrationBuilder.CreateIndex(
                name: "ux_delivery_contact_owner_revoke_operation",
                table: "delivery_contact_consents",
                columns: new[] { "OwnerAccountId", "RevokeOperationId" },
                unique: true,
                filter: "\"RevokeOperationId\" IS NOT NULL");

            migrationBuilder.Sql(DeliveryContactAuthoritySql.Install);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DeliveryContactAuthoritySql.RefusePopulatedDown);
            migrationBuilder.Sql(DeliveryContactAuthoritySql.Remove);
            migrationBuilder.DropTable(
                name: "delivery_contact_leases");

            migrationBuilder.DropTable(
                name: "delivery_contact_consents");

            migrationBuilder.DropTable(
                name: "delivery_contact_revisions");
        }
    }
}
