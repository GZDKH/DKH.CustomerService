using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DKH.CustomerService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceTypesCatalogue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ADR-025 §5: catalogue table for resource types alongside grants.
            // Matches DKH.Platform.Authorization.ResourceAccess.EntityFrameworkCore.Configurations.ResourceTypeConfiguration schema.
            // Raw SQL with IF NOT EXISTS so the migration is idempotent if another
            // bootstrap path (manual seed, Platform helper) has already created it.
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS resource_types (
                    ""Id"" varchar(64) NOT NULL,
                    ""DisplayName"" varchar(128) NOT NULL,
                    ""IsScopeOnly"" boolean NOT NULL,
                    ""ParentScopeTypes"" varchar(64)[] NULL,
                    ""GrantCreatorFullAccess"" boolean NOT NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    CONSTRAINT ""PK_resource_types"" PRIMARY KEY (""Id"")
                );
            ");

            // Primary resource type for this service.
            migrationBuilder.Sql(@"
                INSERT INTO resource_types
                    (""Id"", ""DisplayName"", ""IsScopeOnly"", ""ParentScopeTypes"", ""GrantCreatorFullAccess"", ""CreatedAt"")
                VALUES
                    ('customer', 'Customer', false, ARRAY['storefront']::varchar(64)[], true, NOW())
                ON CONFLICT (""Id"") DO NOTHING;
            ");

            // Parent scope. CustomerService never creates Storefront entities — it only
            // receives scope grants whose resource_type is 'storefront' (set by
            // StorefrontOwnerAssignedHandler via cross-service event). is_scope_only=true.
            migrationBuilder.Sql(@"
                INSERT INTO resource_types
                    (""Id"", ""DisplayName"", ""IsScopeOnly"", ""ParentScopeTypes"", ""GrantCreatorFullAccess"", ""CreatedAt"")
                VALUES
                    ('storefront', 'Storefront', true, NULL, false, NOW())
                ON CONFLICT (""Id"") DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS resource_types;");
        }
    }
}
