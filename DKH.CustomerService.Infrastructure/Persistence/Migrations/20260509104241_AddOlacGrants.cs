using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DKH.CustomerService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOlacGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customer_access_grants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectType = table.Column<byte>(type: "smallint", nullable: false),
                    SubjectId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Permissions = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GrantReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_access_grants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResourceTypes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    IsScopeOnly = table.Column<bool>(type: "boolean", nullable: false),
                    ParentScopeTypes = table.Column<string[]>(type: "text[]", nullable: false),
                    GrantCreatorFullAccess = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_customer_access_grants_subject",
                table: "customer_access_grants",
                columns: new[] { "SubjectType", "SubjectId", "ResourceType" },
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "ux_customer_access_grants_unique",
                table: "customer_access_grants",
                columns: new[] { "ResourceType", "ResourceId", "SubjectType", "SubjectId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_access_grants");

            migrationBuilder.DropTable(
                name: "ResourceTypes");
        }
    }
}
