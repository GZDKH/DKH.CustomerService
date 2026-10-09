using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DKH.CustomerService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivateDatedExperienceJournal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "journal_experience_profile_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    renderer_code = table.Column<string>(type: "text", nullable: false),
                    renderer_version = table.Column<int>(type: "integer", nullable: false),
                    catalog_id = table.Column<Guid>(type: "uuid", nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    canonical_schema = table.Column<string>(type: "text", nullable: false),
                    schema_hash = table.Column<string>(type: "text", nullable: false),
                    creation_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    creator_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_modification_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modifier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    deletion_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_experience_profile_snapshots", x => x.id);
                    table.CheckConstraint("ck_journal_profile_binding", "(renderer_code = 'general' AND renderer_version = 1 AND catalog_id IS NULL AND category_id IS NULL) OR (renderer_code <> 'general' AND catalog_id IS NOT NULL AND category_id IS NOT NULL)");
                    table.CheckConstraint("ck_journal_profile_schema", "schema_version = 1 AND renderer_version > 0 AND char_length(renderer_code) BETWEEN 1 AND 64 AND schema_hash ~ '^[0-9a-f]{64}$' AND octet_length(canonical_schema) <= 131072 AND jsonb_typeof(canonical_schema::jsonb) = 'object'");
                });

            migrationBuilder.CreateTable(
                name: "journal_experience_unknown_references",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_label = table.Column<string>(type: "text", nullable: false),
                    producer_label = table.Column<string>(type: "text", nullable: true),
                    creation_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    creator_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_modification_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modifier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    deletion_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_experience_unknown_references", x => x.id);
                    table.UniqueConstraint("AK_journal_experience_unknown_references_account_id_id", x => new { x.account_id, x.id });
                    table.CheckConstraint("ck_journal_unknown_label", "char_length(owner_label) BETWEEN 1 AND 256 AND char_length(btrim(owner_label)) > 0");
                    table.CheckConstraint("ck_journal_unknown_producer", "producer_label IS NULL OR char_length(producer_label) <= 256");
                    table.ForeignKey(
                        name: "FK_journal_experience_unknown_references_customer_accounts_acc~",
                        column: x => x.account_id,
                        principalTable: "customer_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "journal_experience_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin_storefront_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    unknown_reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    release_id = table.Column<Guid>(type: "uuid", nullable: true),
                    retained_target_label = table.Column<string>(type: "text", nullable: false),
                    occurred_date = table.Column<DateOnly>(type: "date", nullable: false),
                    time_precision = table.Column<int>(type: "integer", nullable: false),
                    local_time_iso = table.Column<string>(type: "text", nullable: true),
                    time_zone = table.Column<string>(type: "text", nullable: true),
                    utc_offset_minutes = table.Column<int>(type: "integer", nullable: true),
                    session_score = table.Column<int>(type: "integer", nullable: true),
                    current_revision = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    creation_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    creator_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_modification_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modifier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    deletion_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_experience_entries", x => x.id);
                    table.UniqueConstraint("AK_journal_experience_entries_account_id_id", x => new { x.account_id, x.id });
                    table.CheckConstraint("ck_journal_entry_ids", "origin_storefront_id <> '00000000-0000-0000-0000-000000000000'::uuid AND (product_id IS NULL OR product_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (release_id IS NULL OR release_id <> '00000000-0000-0000-0000-000000000000'::uuid)");
                    table.CheckConstraint("ck_journal_entry_label", "char_length(retained_target_label) BETWEEN 1 AND 256");
                    table.CheckConstraint("ck_journal_entry_revision", "current_revision > 0");
                    table.CheckConstraint("ck_journal_entry_score", "session_score IS NULL OR session_score BETWEEN 1 AND 5");
                    table.CheckConstraint("ck_journal_entry_target", "((product_id IS NOT NULL)::int + (unknown_reference_id IS NOT NULL)::int) = 1 AND (release_id IS NULL OR product_id IS NOT NULL)");
                    table.CheckConstraint("ck_journal_entry_time", "(time_precision = 1 AND local_time_iso IS NULL AND time_zone IS NULL AND utc_offset_minutes IS NULL) OR (time_precision = 2 AND local_time_iso IS NOT NULL AND utc_offset_minutes IS NOT NULL AND local_time_iso ~ '^([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9][.][0-9]{7}$' AND time_zone IS NOT NULL AND char_length(time_zone) BETWEEN 1 AND 128 AND utc_offset_minutes BETWEEN -1440 AND 1440)");
                    table.ForeignKey(
                        name: "FK_journal_experience_entries_customer_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "customer_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_experience_entries_journal_experience_unknown_refer~",
                        columns: x => new { x.account_id, x.unknown_reference_id },
                        principalTable: "journal_experience_unknown_references",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "journal_experience_revisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    profile_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    canonical_payload = table.Column<string>(type: "text", nullable: false),
                    payload_hash = table.Column<string>(type: "text", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deletion = table.Column<bool>(type: "boolean", nullable: false),
                    creation_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    creator_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_modification_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modifier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    deletion_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_experience_revisions", x => x.id);
                    table.UniqueConstraint("AK_journal_experience_revisions_account_id_entry_id_revision", x => new { x.account_id, x.entry_id, x.revision });
                    table.UniqueConstraint("AK_journal_experience_revisions_account_id_id", x => new { x.account_id, x.id });
                    table.CheckConstraint("ck_journal_revision_payload", "revision > 0 AND schema_version = 1 AND payload_hash ~ '^[0-9a-f]{64}$' AND octet_length(canonical_payload) <= 131072 AND jsonb_typeof(canonical_payload::jsonb) = 'object'");
                    table.ForeignKey(
                        name: "FK_journal_experience_revisions_customer_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "customer_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_experience_revisions_journal_experience_entries_acc~",
                        columns: x => new { x.account_id, x.entry_id },
                        principalTable: "journal_experience_entries",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_experience_revisions_journal_experience_profile_sna~",
                        column: x => x.profile_snapshot_id,
                        principalTable: "journal_experience_profile_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "journal_experience_mutation_receipts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<int>(type: "integer", nullable: false),
                    idempotency_key = table.Column<Guid>(type: "uuid", nullable: false),
                    request_hash = table.Column<string>(type: "text", nullable: false),
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    result_revision = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    creation_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    creator_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_modification_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modifier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    deletion_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_experience_mutation_receipts", x => x.id);
                    table.CheckConstraint("ck_journal_receipt_key", "idempotency_key <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_journal_receipt_shape", "operation IN (1,2,3,4) AND result_revision > 0 AND request_hash ~ '^[0-9a-f]{64}$' AND expires_at_utc = created_at_utc + interval '24 hours'");
                    table.ForeignKey(
                        name: "FK_journal_experience_mutation_receipts_customer_accounts_acco~",
                        column: x => x.account_id,
                        principalTable: "customer_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_experience_mutation_receipts_journal_experience_rev~",
                        columns: x => new { x.account_id, x.entry_id, x.result_revision },
                        principalTable: "journal_experience_revisions",
                        principalColumns: new[] { "account_id", "entry_id", "revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "journal_experience_observations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    value_type = table.Column<int>(type: "integer", nullable: false),
                    text_value = table.Column<string>(type: "text", nullable: true),
                    decimal_value = table.Column<decimal>(type: "numeric", nullable: true),
                    integer_value = table.Column<long>(type: "bigint", nullable: true),
                    boolean_value = table.Column<bool>(type: "boolean", nullable: true),
                    unit_code = table.Column<string>(type: "text", nullable: true),
                    row_id = table.Column<Guid>(type: "uuid", nullable: true),
                    row_ordinal = table.Column<int>(type: "integer", nullable: true),
                    option_id = table.Column<Guid>(type: "uuid", nullable: true),
                    creation_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    creator_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_modification_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modifier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    deletion_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_experience_observations", x => x.id);
                    table.CheckConstraint("ck_journal_observation_decimal", "decimal_value IS NULL OR (decimal_value BETWEEN -999999999999.999999 AND 999999999999.999999 AND decimal_value = round(decimal_value, 6))");
                    table.CheckConstraint("ck_journal_observation_ids", "definition_id <> '00000000-0000-0000-0000-000000000000'::uuid AND (row_id IS NULL OR row_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (option_id IS NULL OR option_id <> '00000000-0000-0000-0000-000000000000'::uuid)");
                    table.CheckConstraint("ck_journal_observation_limits", "(text_value IS NULL OR char_length(text_value) <= 2000) AND (unit_code IS NULL OR char_length(unit_code) <= 32)");
                    table.CheckConstraint("ck_journal_observation_row", "(row_id IS NULL AND row_ordinal IS NULL) OR (row_id IS NOT NULL AND row_ordinal IS NOT NULL AND row_ordinal BETWEEN 1 AND 12)");
                    table.CheckConstraint("ck_journal_observation_shape", "role IN (1,2) AND num_nonnulls(text_value, decimal_value, integer_value, boolean_value) = 1 AND ((value_type = 1 AND text_value IS NOT NULL) OR (value_type = 2 AND decimal_value IS NOT NULL) OR (value_type = 3 AND integer_value IS NOT NULL) OR (value_type = 4 AND boolean_value IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_journal_experience_observations_customer_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "customer_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_experience_observations_journal_experience_revision~",
                        columns: x => new { x.account_id, x.revision_id },
                        principalTable: "journal_experience_revisions",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "journal_experience_profile_snapshots",
                columns: new[] { "id", "canonical_schema", "catalog_id", "category_id", "creation_time", "creator_id", "deleter_id", "deletion_time", "is_deleted", "last_modification_time", "last_modifier_id", "renderer_code", "renderer_version", "schema_hash", "schema_version" },
                values: new object[] { new Guid("f9eb970a-7e55-54f3-9aaa-74e3383ee0de"), "{\"catalogId\":null,\"categoryId\":null,\"definitions\":[],\"rendererCode\":\"general\",\"rendererVersion\":1,\"schemaVersion\":1}", null, null, new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, false, null, null, "general", 1, "f9eb970a7e5524f39aaa74e3383ee0ded5e8f17450a9fc58956ccd0cfe512371", 1 });

            migrationBuilder.CreateIndex(
                name: "IX_journal_experience_entries_account_id_unknown_reference_id",
                table: "journal_experience_entries",
                columns: new[] { "account_id", "unknown_reference_id" });

            migrationBuilder.CreateIndex(
                name: "ix_journal_owner_date_id",
                table: "journal_experience_entries",
                columns: new[] { "account_id", "occurred_date", "id" },
                descending: new[] { false, true, true },
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_journal_owner_product_date_id",
                table: "journal_experience_entries",
                columns: new[] { "account_id", "product_id", "occurred_date", "id" },
                descending: new[] { false, false, true, true },
                filter: "is_deleted = false AND product_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_journal_experience_mutation_receipts_account_id_entry_id_re~",
                table: "journal_experience_mutation_receipts",
                columns: new[] { "account_id", "entry_id", "result_revision" });

            migrationBuilder.CreateIndex(
                name: "ix_journal_receipt_expiry",
                table: "journal_experience_mutation_receipts",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "ux_journal_owner_operation_key",
                table: "journal_experience_mutation_receipts",
                columns: new[] { "account_id", "operation", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_journal_experience_observations_account_id_revision_id",
                table: "journal_experience_observations",
                columns: new[] { "account_id", "revision_id" });

            migrationBuilder.CreateIndex(
                name: "ux_journal_observation_null_row",
                table: "journal_experience_observations",
                columns: new[] { "revision_id", "definition_id", "role" },
                unique: true,
                filter: "row_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_journal_observation_row",
                table: "journal_experience_observations",
                columns: new[] { "revision_id", "definition_id", "role", "row_id" },
                unique: true,
                filter: "row_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_journal_profile_schema_hash",
                table: "journal_experience_profile_snapshots",
                column: "schema_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_journal_experience_revisions_profile_snapshot_id",
                table: "journal_experience_revisions",
                column: "profile_snapshot_id");

            migrationBuilder.CreateIndex(
                name: "ux_journal_entry_revision",
                table: "journal_experience_revisions",
                columns: new[] { "entry_id", "revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // C2 explicitly requires empty-disposable-only Down; production rollback retains owner data.
            migrationBuilder.Sql("""
                DO $journal_down_guard$
                BEGIN
                    IF current_database() <> 'dkh_journal_fixture'
                       AND current_database() NOT LIKE 'dkh_journal_fixture_%' THEN
                        RAISE EXCEPTION 'Journal Down is restricted to an explicitly disposable fixture database';
                    END IF;
                    IF EXISTS (SELECT 1 FROM journal_experience_entries)
                       OR EXISTS (SELECT 1 FROM journal_experience_revisions)
                       OR EXISTS (SELECT 1 FROM journal_experience_unknown_references)
                       OR EXISTS (SELECT 1 FROM journal_experience_observations)
                       OR EXISTS (SELECT 1 FROM journal_experience_mutation_receipts)
                       OR EXISTS (SELECT 1 FROM journal_experience_profile_snapshots
                                  WHERE id <> 'f9eb970a-7e55-54f3-9aaa-74e3383ee0de'::uuid) THEN
                        RAISE EXCEPTION 'Journal Down refused: retain data and disable new writers instead';
                    END IF;
                END
                $journal_down_guard$;
                """);

            migrationBuilder.DropTable(
                name: "journal_experience_mutation_receipts");

            migrationBuilder.DropTable(
                name: "journal_experience_observations");

            migrationBuilder.DropTable(
                name: "journal_experience_revisions");

            migrationBuilder.DropTable(
                name: "journal_experience_entries");

            migrationBuilder.DropTable(
                name: "journal_experience_profile_snapshots");

            migrationBuilder.DropTable(
                name: "journal_experience_unknown_references");
        }
    }
}
