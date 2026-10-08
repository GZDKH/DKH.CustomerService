using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DKH.CustomerService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LimitActiveProductExperienceSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_product_collection_experiences_CollectionItemId",
                table: "product_collection_experiences");

            migrationBuilder.CreateIndex(
                name: "IX_product_collection_experiences_CollectionItemId",
                table: "product_collection_experiences",
                column: "CollectionItemId",
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_product_collection_experiences_CollectionItemId",
                table: "product_collection_experiences");

            migrationBuilder.CreateIndex(
                name: "IX_product_collection_experiences_CollectionItemId",
                table: "product_collection_experiences",
                column: "CollectionItemId",
                unique: true);
        }
    }
}
