using DKH.CustomerService.Domain.Entities.CustomerAccount;
using DKH.CustomerService.Domain.Entities.ExperienceJournal;
using DKH.Platform.Domain.Entities.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DKH.CustomerService.Infrastructure.Persistence.Configurations;

internal static class ExperienceJournalMapping
{
    public static void Audits<T>(EntityTypeBuilder<T> builder) where T : FullAuditedEntityWithKey<Guid>
    {
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Id).HasColumnName("id");
        builder.Property(row => row.IsDeleted).HasColumnName("is_deleted");
        builder.Property("CreationTime").HasColumnName("creation_time");
        builder.Property("CreatorId").HasColumnName("creator_id");
        builder.Property("LastModificationTime").HasColumnName("last_modification_time");
        builder.Property("LastModifierId").HasColumnName("last_modifier_id");
        builder.Property("DeletionTime").HasColumnName("deletion_time");
        builder.Property("DeleterId").HasColumnName("deleter_id");
        builder.HasQueryFilter(row => !row.IsDeleted);
    }
}

public sealed class ExperienceEntryConfiguration : IEntityTypeConfiguration<ExperienceEntryEntity>
{
    public void Configure(EntityTypeBuilder<ExperienceEntryEntity> builder)
    {
        ExperienceJournalMapping.Audits(builder);
        builder.ToTable("journal_experience_entries", table =>
        {
            table.HasCheckConstraint("ck_journal_entry_target", "((product_id IS NOT NULL)::int + (unknown_reference_id IS NOT NULL)::int) = 1 AND (release_id IS NULL OR product_id IS NOT NULL)");
            table.HasCheckConstraint("ck_journal_entry_revision", "current_revision > 0");
            table.HasCheckConstraint("ck_journal_entry_score", "session_score IS NULL OR session_score BETWEEN 1 AND 5");
            table.HasCheckConstraint("ck_journal_entry_label", "char_length(retained_target_label) BETWEEN 1 AND 256");
            table.HasCheckConstraint("ck_journal_entry_ids", "origin_storefront_id <> '00000000-0000-0000-0000-000000000000'::uuid AND (product_id IS NULL OR product_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (release_id IS NULL OR release_id <> '00000000-0000-0000-0000-000000000000'::uuid)");
            table.HasCheckConstraint("ck_journal_entry_time", "(time_precision = 1 AND local_time_iso IS NULL AND time_zone IS NULL AND utc_offset_minutes IS NULL) OR (time_precision = 2 AND local_time_iso IS NOT NULL AND utc_offset_minutes IS NOT NULL AND local_time_iso ~ '^([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9][.][0-9]{7}$' AND time_zone IS NOT NULL AND char_length(time_zone) BETWEEN 1 AND 128 AND utc_offset_minutes BETWEEN -1440 AND 1440)");
        });
        builder.Property(row => row.AccountId).HasColumnName("account_id");
        builder.Property(row => row.OriginStorefrontId).HasColumnName("origin_storefront_id");
        builder.Property(row => row.ProductId).HasColumnName("product_id");
        builder.Property(row => row.UnknownReferenceId).HasColumnName("unknown_reference_id");
        builder.Property(row => row.ReleaseId).HasColumnName("release_id");
        builder.Property(row => row.RetainedTargetLabel).HasColumnName("retained_target_label").HasColumnType("text").IsRequired();
        builder.Property(row => row.OccurredDate).HasColumnName("occurred_date").HasColumnType("date");
        builder.Property(row => row.TimePrecision).HasColumnName("time_precision");
        builder.Property(row => row.LocalTimeIso).HasColumnName("local_time_iso").HasColumnType("text");
        builder.Property(row => row.TimeZone).HasColumnName("time_zone").HasColumnType("text");
        builder.Property(row => row.UtcOffsetMinutes).HasColumnName("utc_offset_minutes");
        builder.Property(row => row.SessionScore).HasColumnName("session_score");
        builder.Property(row => row.CurrentRevision).HasColumnName("current_revision").IsConcurrencyToken();
        builder.Property(row => row.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(row => row.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.HasAlternateKey(row => new { row.AccountId, row.Id });
        builder.HasOne<CustomerAccountEntity>().WithMany().HasForeignKey(row => row.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ExperienceUnknownReferenceEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.UnknownReferenceId })
            .HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(row => row.Revisions).WithOne().HasForeignKey(row => new { row.AccountId, row.EntryId })
            .HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(row => row.Revisions).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(row => new { row.AccountId, row.OccurredDate, row.Id }).IsDescending(false, true, true)
            .HasFilter("is_deleted = false").HasDatabaseName("ix_journal_owner_date_id");
        builder.HasIndex(row => new { row.AccountId, row.ProductId, row.OccurredDate, row.Id }).IsDescending(false, false, true, true)
            .HasFilter("is_deleted = false AND product_id IS NOT NULL").HasDatabaseName("ix_journal_owner_product_date_id");
    }
}

public sealed class ExperienceUnknownReferenceConfiguration : IEntityTypeConfiguration<ExperienceUnknownReferenceEntity>
{
    public void Configure(EntityTypeBuilder<ExperienceUnknownReferenceEntity> builder)
    {
        ExperienceJournalMapping.Audits(builder);
        builder.ToTable("journal_experience_unknown_references", table =>
        {
            table.HasCheckConstraint("ck_journal_unknown_label", "char_length(owner_label) BETWEEN 1 AND 256 AND char_length(btrim(owner_label)) > 0");
            table.HasCheckConstraint("ck_journal_unknown_producer", "producer_label IS NULL OR char_length(producer_label) <= 256");
        });
        builder.Property(row => row.AccountId).HasColumnName("account_id");
        builder.Property(row => row.OwnerLabel).HasColumnName("owner_label").HasColumnType("text").IsRequired();
        builder.Property(row => row.ProducerLabel).HasColumnName("producer_label").HasColumnType("text");
        builder.HasAlternateKey(row => new { row.AccountId, row.Id });
        builder.HasOne<CustomerAccountEntity>().WithMany().HasForeignKey(row => row.AccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ExperienceProfileSnapshotConfiguration : IEntityTypeConfiguration<ExperienceProfileSnapshotEntity>
{
    public void Configure(EntityTypeBuilder<ExperienceProfileSnapshotEntity> builder)
    {
        ExperienceJournalMapping.Audits(builder);
        builder.ToTable("journal_experience_profile_snapshots", table =>
        {
            table.HasCheckConstraint("ck_journal_profile_binding", "(renderer_code = 'general' AND renderer_version = 1 AND catalog_id IS NULL AND category_id IS NULL) OR (renderer_code <> 'general' AND catalog_id IS NOT NULL AND category_id IS NOT NULL)");
            table.HasCheckConstraint("ck_journal_profile_schema", "schema_version = 1 AND renderer_version > 0 AND char_length(renderer_code) BETWEEN 1 AND 64 AND schema_hash ~ '^[0-9a-f]{64}$' AND octet_length(canonical_schema) <= 131072 AND jsonb_typeof(canonical_schema::jsonb) = 'object'");
        });
        builder.Property(row => row.RendererCode).HasColumnName("renderer_code").HasColumnType("text").IsRequired();
        builder.Property(row => row.RendererVersion).HasColumnName("renderer_version");
        builder.Property(row => row.CatalogId).HasColumnName("catalog_id");
        builder.Property(row => row.CategoryId).HasColumnName("category_id");
        builder.Property(row => row.SchemaVersion).HasColumnName("schema_version");
        builder.Property(row => row.SchemaHash).HasColumnName("schema_hash").HasColumnType("text").IsRequired();
        builder.Property(row => row.CanonicalSchema).HasColumnName("canonical_schema").HasColumnType("text").IsRequired();
        builder.HasIndex(row => row.SchemaHash).IsUnique().HasDatabaseName("ux_journal_profile_schema_hash");
        builder.HasData(new
        {
            Id = ExperienceProfileSnapshotEntity.GeneralV1Id,
            RendererCode = "general",
            RendererVersion = 1,
            CatalogId = (Guid?)null,
            CategoryId = (Guid?)null,
            SchemaVersion = 1,
            SchemaHash = ExperienceProfileSnapshotEntity.GeneralV1SchemaHash,
            CanonicalSchema = ExperienceProfileSnapshotEntity.GeneralV1CanonicalSchema,
            IsDeleted = false,
            CreationTime = DateTime.UnixEpoch,
        });
    }
}

public sealed class ExperienceRevisionConfiguration : IEntityTypeConfiguration<ExperienceRevisionEntity>
{
    public void Configure(EntityTypeBuilder<ExperienceRevisionEntity> builder)
    {
        ExperienceJournalMapping.Audits(builder);
        builder.ToTable("journal_experience_revisions", table => table.HasCheckConstraint("ck_journal_revision_payload", "revision > 0 AND schema_version = 1 AND payload_hash ~ '^[0-9a-f]{64}$' AND octet_length(canonical_payload) <= 131072 AND jsonb_typeof(canonical_payload::jsonb) = 'object'"));
        builder.Property(row => row.AccountId).HasColumnName("account_id");
        builder.Property(row => row.EntryId).HasColumnName("entry_id");
        builder.Property(row => row.Revision).HasColumnName("revision");
        builder.Property(row => row.ProfileSnapshotId).HasColumnName("profile_snapshot_id");
        builder.Property(row => row.SchemaVersion).HasColumnName("schema_version");
        builder.Property(row => row.CanonicalPayload).HasColumnName("canonical_payload").HasColumnType("text").IsRequired();
        builder.Property(row => row.PayloadHash).HasColumnName("payload_hash").HasColumnType("text").IsRequired();
        builder.Property(row => row.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(row => row.IsDeletion).HasColumnName("is_deletion");
        builder.HasAlternateKey(row => new { row.AccountId, row.Id });
        builder.HasAlternateKey(row => new { row.AccountId, row.EntryId, row.Revision });
        builder.HasIndex(row => new { row.EntryId, row.Revision }).IsUnique().HasDatabaseName("ux_journal_entry_revision");
        builder.HasOne<CustomerAccountEntity>().WithMany().HasForeignKey(row => row.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ExperienceProfileSnapshotEntity>().WithMany().HasForeignKey(row => row.ProfileSnapshotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(row => row.Observations).WithOne().HasForeignKey(row => new { row.AccountId, row.RevisionId })
            .HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(row => row.Observations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class ExperienceRevisionObservationConfiguration : IEntityTypeConfiguration<ExperienceRevisionObservationEntity>
{
    public void Configure(EntityTypeBuilder<ExperienceRevisionObservationEntity> builder)
    {
        ExperienceJournalMapping.Audits(builder);
        builder.ToTable("journal_experience_observations", table =>
        {
            table.HasCheckConstraint("ck_journal_observation_shape", "role IN (1,2) AND num_nonnulls(text_value, decimal_value, integer_value, boolean_value) = 1 AND ((value_type = 1 AND text_value IS NOT NULL) OR (value_type = 2 AND decimal_value IS NOT NULL) OR (value_type = 3 AND integer_value IS NOT NULL) OR (value_type = 4 AND boolean_value IS NOT NULL))");
            table.HasCheckConstraint("ck_journal_observation_limits", "(text_value IS NULL OR char_length(text_value) <= 2000) AND (unit_code IS NULL OR char_length(unit_code) <= 32)");
            table.HasCheckConstraint("ck_journal_observation_decimal", "decimal_value IS NULL OR (decimal_value BETWEEN -999999999999.999999 AND 999999999999.999999 AND decimal_value = round(decimal_value, 6))");
            table.HasCheckConstraint("ck_journal_observation_row", "(row_id IS NULL AND row_ordinal IS NULL) OR (row_id IS NOT NULL AND row_ordinal IS NOT NULL AND row_ordinal BETWEEN 1 AND 12)");
            table.HasCheckConstraint("ck_journal_observation_ids", "definition_id <> '00000000-0000-0000-0000-000000000000'::uuid AND (row_id IS NULL OR row_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (option_id IS NULL OR option_id <> '00000000-0000-0000-0000-000000000000'::uuid)");
        });
        builder.Property(row => row.AccountId).HasColumnName("account_id");
        builder.Property(row => row.RevisionId).HasColumnName("revision_id");
        builder.Property(row => row.DefinitionId).HasColumnName("definition_id");
        builder.Property(row => row.Role).HasColumnName("role");
        builder.Property(row => row.ValueType).HasColumnName("value_type");
        builder.Property(row => row.TextValue).HasColumnName("text_value").HasColumnType("text");
        // Unconstrained numeric plus representability CHECK rejects excess fractional precision before any typmod rounding.
        builder.Property(row => row.DecimalValue).HasColumnName("decimal_value").HasColumnType("numeric");
        builder.Property(row => row.IntegerValue).HasColumnName("integer_value");
        builder.Property(row => row.BooleanValue).HasColumnName("boolean_value");
        builder.Property(row => row.UnitCode).HasColumnName("unit_code").HasColumnType("text");
        builder.Property(row => row.RowId).HasColumnName("row_id");
        builder.Property(row => row.RowOrdinal).HasColumnName("row_ordinal");
        builder.Property(row => row.OptionId).HasColumnName("option_id");
        builder.HasOne<CustomerAccountEntity>().WithMany().HasForeignKey(row => row.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.RevisionId, row.DefinitionId, row.Role, row.RowId }).IsUnique()
            .HasFilter("row_id IS NOT NULL").HasDatabaseName("ux_journal_observation_row");
        builder.HasIndex(row => new { row.RevisionId, row.DefinitionId, row.Role }).IsUnique()
            .HasFilter("row_id IS NULL").HasDatabaseName("ux_journal_observation_null_row");
    }
}

public sealed class ExperienceMutationReceiptConfiguration : IEntityTypeConfiguration<ExperienceMutationReceiptEntity>
{
    public void Configure(EntityTypeBuilder<ExperienceMutationReceiptEntity> builder)
    {
        ExperienceJournalMapping.Audits(builder);
        builder.ToTable("journal_experience_mutation_receipts", table =>
        {
            table.HasCheckConstraint("ck_journal_receipt_shape", "operation IN (1,2,3,4) AND result_revision > 0 AND request_hash ~ '^[0-9a-f]{64}$' AND expires_at_utc = created_at_utc + interval '24 hours'");
            table.HasCheckConstraint("ck_journal_receipt_key", "idempotency_key <> '00000000-0000-0000-0000-000000000000'::uuid");
        });
        builder.Property(row => row.AccountId).HasColumnName("account_id");
        builder.Property(row => row.Operation).HasColumnName("operation");
        builder.Property(row => row.IdempotencyKey).HasColumnName("idempotency_key");
        builder.Property(row => row.RequestHash).HasColumnName("request_hash").HasColumnType("text").IsRequired();
        builder.Property(row => row.EntryId).HasColumnName("entry_id");
        builder.Property(row => row.ResultRevision).HasColumnName("result_revision");
        builder.Property(row => row.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(row => row.ExpiresAtUtc).HasColumnName("expires_at_utc");
        builder.Ignore(row => row.OriginalResult);
        builder.HasIndex(row => new { row.AccountId, row.Operation, row.IdempotencyKey }).IsUnique().HasDatabaseName("ux_journal_owner_operation_key");
        builder.HasIndex(row => row.ExpiresAtUtc).HasDatabaseName("ix_journal_receipt_expiry");
        builder.HasOne<CustomerAccountEntity>().WithMany().HasForeignKey(row => row.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ExperienceRevisionEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.EntryId, row.ResultRevision })
            .HasPrincipalKey(row => new { row.AccountId, row.EntryId, row.Revision }).OnDelete(DeleteBehavior.Restrict);
    }
}
