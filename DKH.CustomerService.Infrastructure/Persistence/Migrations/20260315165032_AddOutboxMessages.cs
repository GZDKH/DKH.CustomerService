using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DKH.CustomerService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS outbox_messages (
                    id uuid NOT NULL,
                    type character varying(512) NOT NULL,
                    payload text NOT NULL,
                    occurred_on_utc timestamp with time zone NOT NULL,
                    attempts integer NOT NULL DEFAULT 0,
                    processed_on_utc timestamp with time zone,
                    error text,
                    CONSTRAINT "PK_outbox_messages" PRIMARY KEY (id)
                );

                CREATE INDEX IF NOT EXISTS ix_outbox_messages_processed_on_utc
                    ON outbox_messages (processed_on_utc)
                    WHERE processed_on_utc IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS outbox_messages;");
        }
    }
}
