using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DKH.CustomerService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivateStructuredProductExperience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_collection_experiences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExperiencedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PersonalText = table.Column<string>(type: "character varying(7000)", maxLength: 7000, nullable: true),
                    Recommendation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_product_collection_experiences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_collection_experiences_product_collection_items_Col~",
                        column: x => x.CollectionItemId,
                        principalTable: "product_collection_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_experience_observations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExperienceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ValueType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TextValue = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DecimalValue = table.Column<double>(type: "double precision", precision: 18, scale: 6, nullable: true),
                    IntegerValue = table.Column<long>(type: "bigint", nullable: true),
                    BooleanValue = table.Column<bool>(type: "boolean", nullable: true),
                    UnitCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
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
                    table.PrimaryKey("PK_product_experience_observations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_experience_observations_product_collection_experien~",
                        column: x => x.ExperienceId,
                        principalTable: "product_collection_experiences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_experience_tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExperienceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_product_experience_tags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_experience_tags_product_collection_experiences_Expe~",
                        column: x => x.ExperienceId,
                        principalTable: "product_collection_experiences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_collection_experiences_CollectionItemId",
                table: "product_collection_experiences",
                column: "CollectionItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_experience_observations_ExperienceId_DefinitionId_R~",
                table: "product_experience_observations",
                columns: new[] { "ExperienceId", "DefinitionId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_experience_tags_ExperienceId_Value",
                table: "product_experience_tags",
                columns: new[] { "ExperienceId", "Value" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_product_collection_items_customer_profiles_CustomerId",
                table: "product_collection_items",
                column: "CustomerId",
                principalTable: "customer_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_product_collection_items_customer_profiles_CustomerId",
                table: "product_collection_items");

            migrationBuilder.DropTable(
                name: "product_experience_observations");

            migrationBuilder.DropTable(
                name: "product_experience_tags");

            migrationBuilder.DropTable(
                name: "product_collection_experiences");
        }
    }
}
