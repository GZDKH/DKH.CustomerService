using DKH.CustomerService.Application.CustomerAccounts;
using DKH.CustomerService.Application.ExperienceJournal;
using DKH.CustomerService.Domain.Entities.ExperienceJournal;
using DKH.CustomerService.Domain.Entities.ProductCollection;
using DKH.CustomerService.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DKH.CustomerService.IntegrationTests.Integration.Journal;

[Trait("Category", "Integration")]
public sealed class JournalPersistenceTests(JournalHostFixture fixture) : IClassFixture<JournalHostFixture>
{
    [Theory]
    [InlineData("score-zero", "ck_journal_entry_score")]
    [InlineData("score-six", "ck_journal_entry_score")]
    [InlineData("missing-target", "ck_journal_entry_target")]
    [InlineData("partial-time", "ck_journal_entry_time")]
    public async Task ActualPostgreSqlRejectsInvalidHeaderValuesAsync(string rule, string constraint)
    {
        var account = await fixture.SeedAccountAsync();
        var saved = await fixture.MutateAsync(Create(Draft(Guid.NewGuid())), fixture.Token(account.IdentitySubject));
        await fixture.ReadAsync(async db =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            FormattableString sql = rule switch
            {
                "score-zero" => $"UPDATE journal_experience_entries SET session_score = 0 WHERE id = {saved.EntryId}",
                "score-six" => $"UPDATE journal_experience_entries SET session_score = 6 WHERE id = {saved.EntryId}",
                "missing-target" => $"UPDATE journal_experience_entries SET product_id = NULL WHERE id = {saved.EntryId}",
                _ => $"UPDATE journal_experience_entries SET local_time_iso = '00:00:00.0000000' WHERE id = {saved.EntryId}",
            };
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(sql));
            error.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
            error.ConstraintName.Should().Be(constraint);
            await transaction.RollbackAsync();
            return true;
        });
    }

    [Fact]
    public async Task ActualPostgreSqlSchemaHasRestrictedOwnerRelationsExactTypesAndKeysetIndexesAsync()
    {
        var numeric = await fixture.ReadAsync(db => db.Database.SqlQueryRaw<string>("""
            SELECT format_type(atttypid, atttypmod) AS "Value" FROM pg_attribute
            WHERE attrelid = 'journal_experience_observations'::regclass AND attname = 'decimal_value'
            """).SingleAsync());
        numeric.Should().Be("numeric", "a typmod would round before the exact representability CHECK");
        var dateType = await fixture.ReadAsync(db => db.Database.SqlQueryRaw<string>("""
            SELECT format_type(atttypid, atttypmod) AS "Value" FROM pg_attribute
            WHERE attrelid = 'journal_experience_entries'::regclass AND attname = 'occurred_date'
            """).SingleAsync());
        dateType.Should().Be("date");
        var legacyType = await fixture.ReadAsync(db => db.Database.SqlQueryRaw<string>("""
            SELECT format_type(atttypid, atttypmod) AS "Value" FROM pg_attribute
            WHERE attrelid = 'product_experience_observations'::regclass AND attname = 'DecimalValue'
            """).SingleAsync());
        legacyType.Should().Be("double precision");
        var foreignDeletes = await fixture.ReadAsync(db => db.Database.SqlQueryRaw<string>("""
            SELECT confdeltype::text AS "Value" FROM pg_constraint
            WHERE contype = 'f' AND conrelid IN (SELECT oid FROM pg_class WHERE relname LIKE 'journal_experience_%')
            """).ToArrayAsync());
        foreignDeletes.Should().NotBeEmpty().And.OnlyContain(value => value == "r");
        var indexes = await fixture.ReadAsync(db => db.Database.SqlQueryRaw<string>("""
            SELECT indexdef AS "Value" FROM pg_indexes WHERE indexname IN
            ('ix_journal_owner_date_id', 'ix_journal_owner_product_date_id', 'ux_journal_observation_null_row',
             'ux_journal_observation_row', 'ux_journal_entry_revision', 'ux_journal_owner_operation_key')
            """).ToArrayAsync());
        indexes.Should().HaveCount(6);
        indexes.Should().Contain(value => value.Contains("occurred_date DESC, id DESC", StringComparison.Ordinal));
        indexes.Should().Contain(value => value.Contains("row_id IS NULL", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("revision")]
    [InlineData("profile")]
    [InlineData("unknown")]
    [InlineData("receipt")]
    [InlineData("header")]
    public async Task OrdinaryHistoricalAndUnpairedHeaderWritesAreRefusedAsync(string rowKind)
    {
        var account = await fixture.SeedAccountAsync();
        var saved = await fixture.MutateAsync(Create(Draft(null)), fixture.Token(account.IdentitySubject));
        var before = await fixture.ReadAsync(db => db.ExperienceRevisions.SingleAsync(row => row.EntryId == saved.EntryId));
        await fixture.ReadAsync(async db =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            switch (rowKind)
            {
                case "revision":
                    var revision = await db.ExperienceRevisions.SingleAsync(row => row.EntryId == saved.EntryId);
                    db.Entry(revision).Property(row => row.CanonicalPayload).CurrentValue = "{}";
                    break;
                case "profile":
                    var profile = await db.ExperienceProfileSnapshots.SingleAsync(row => row.Id == ExperienceProfileSnapshotEntity.GeneralV1Id);
                    db.Entry(profile).Property(row => row.RendererVersion).CurrentValue = 2;
                    break;
                case "unknown":
                    db.ExperienceUnknownReferences.Remove(await db.ExperienceUnknownReferences.SingleAsync(row => row.AccountId == account.Id));
                    break;
                case "receipt":
                    db.ExperienceMutationReceipts.Remove(await db.ExperienceMutationReceipts.SingleAsync(row => row.AccountId == account.Id));
                    break;
                case "header":
                    var header = await db.ExperienceEntries.SingleAsync(row => row.Id == saved.EntryId);
                    db.Entry(header).Property(row => row.SessionScore).CurrentValue = 5;
                    break;
            }

            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            // All sync overloads use the same Platform audit hook as async.
            Action syncWrite = () => db.SaveChanges();
            syncWrite.Should().Throw<InvalidOperationException>();
            await transaction.RollbackAsync();
            return true;
        });
        (await fixture.ReadAsync(db => db.ExperienceRevisions.SingleAsync(row => row.EntryId == saved.EntryId)))
            .CanonicalPayload.Should().Be(before.CanonicalPayload);
        (await fixture.ReadAsync(db => db.ExperienceEntries.SingleAsync(row => row.Id == saved.EntryId))).SessionScore.Should().BeNull();
        (await fixture.ReadAsync(db => db.ExperienceMutationReceipts.CountAsync(row => row.AccountId == account.Id))).Should().Be(1);
    }

    [Theory]
    [InlineData("0.0000001")]
    [InlineData("999999999999.9999991")]
    [InlineData("1000000000000")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    public async Task PostgreSqlRejectsUnrepresentableNumericBeforeRoundingAsync(string value)
    {
        var account = await fixture.SeedAccountAsync();
        var saved = await fixture.MutateAsync(Create(Draft(Guid.NewGuid())), fixture.Token(account.IdentitySubject));
        var revision = await fixture.ReadAsync(db => db.ExperienceRevisions.SingleAsync(row => row.EntryId == saved.EntryId));
        await fixture.ReadAsync(async db =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO journal_experience_observations
                    (id, account_id, revision_id, definition_id, role, value_type, decimal_value, creation_time, is_deleted)
                VALUES ({Guid.NewGuid()}, {account.Id}, {revision.Id}, {Guid.NewGuid()}, 1, 2, {value}::numeric, now(), false)
                """));
            error.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
            error.ConstraintName.Should().Be("ck_journal_observation_decimal");
            await transaction.RollbackAsync();
            return true;
        });
    }

    [Theory]
    [InlineData(-240)]
    [InlineData(-300)]
    public async Task FrozenNumericAndTickPrecisionRoundTripAndDeleteWithoutLoadedObservationsAsync(int selectedOffset)
    {
        var account = await fixture.SeedAccountAsync();
        var catalog = Guid.NewGuid();
        var category = Guid.NewGuid();
        var definition = Guid.NewGuid();
        var profile = ExperienceProfileSnapshotEntity.CreateFrozen(catalog, category, "category", 1,
            [new ExperienceFrozenDefinition(definition, catalog, category, "Value", ProductExperienceObservationRole.Observation,
                ProductExperienceObservationValueType.Decimal, false, null, [], [])]);
        await fixture.ReadAsync(async db =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            db.ExperienceProfileSnapshots.Add(profile);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        });
        var content = ExperienceContent.Create(null, 1, [],
            [ExperienceObservationValue.Create(definition, ProductExperienceObservationRole.Observation,
                ProductExperienceObservationValueType.Decimal, decimalValue: ExperienceObservationValue.MaximumDecimal)]);
        var occurrence = ExperienceOccurrence.Create(new DateOnly(2026, 11, 1), ExperienceTimePrecision.LocalTime,
            new TimeOnly(1, 30).Add(TimeSpan.FromTicks(1)), "America/New_York", selectedOffset);
        var draft = Draft(Guid.NewGuid()) with
        {
            ProfileSnapshotId = profile.Id,
            CatalogId = catalog,
            CategoryId = category,
            Content = content,
            Occurrence = occurrence
        };
        var saved = await fixture.MutateAsync(Create(draft), fixture.Token(account.IdentitySubject));
        var header = await fixture.ReadAsync(db => db.ExperienceEntries.SingleAsync(row => row.Id == saved.EntryId));
        header.LocalTimeIso.Should().Be("01:30:00.0000001");
        header.UtcOffsetMinutes.Should().Be(selectedOffset);
        var first = await fixture.ReadAsync(db => db.ExperienceRevisions.Include(row => row.Observations).SingleAsync(row => row.EntryId == saved.EntryId));
        first.Observations.Single().DecimalValue.Should().Be(ExperienceObservationValue.MaximumDecimal);
        await fixture.MutateAsync(new MutateJournalCommand(new CustomerAccountIdentity("ignored", "ignored"), Guid.NewGuid(),
            ExperienceMutationOperation.Delete, Guid.NewGuid(), saved.EntryId, 1, null), fixture.Token(account.IdentitySubject));
        var deleted = await fixture.ReadAsync(db => db.ExperienceRevisions.Include(row => row.Observations)
            .SingleAsync(row => row.EntryId == saved.EntryId && row.Revision == 2));
        deleted.IsDeletion.Should().BeTrue();
        deleted.Observations.Single().DecimalValue.Should().Be(first.Observations.Single().DecimalValue);
        deleted.ReadCanonicalPayload().Should().Contain("999999999999.999999");
    }

    [Fact]
    public async Task PostgreSqlRejectsCrossOwnerReferenceAndDuplicateNullRowAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var other = await fixture.SeedAccountAsync();
        var saved = await fixture.MutateAsync(Create(Draft(Guid.NewGuid())), fixture.Token(account.IdentitySubject));
        var revision = await fixture.ReadAsync(db => db.ExperienceRevisions.SingleAsync(row => row.EntryId == saved.EntryId));
        await fixture.ReadAsync(async db =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var foreign = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO journal_experience_observations
                    (id, account_id, revision_id, definition_id, role, value_type, boolean_value, creation_time, is_deleted)
                VALUES ({Guid.NewGuid()}, {other.Id}, {revision.Id}, {Guid.NewGuid()}, 1, 4, false, now(), false)
                """));
            foreign.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
            await transaction.RollbackAsync();
            return true;
        });
        await fixture.ReadAsync(async db =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var definition = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO journal_experience_observations
                    (id, account_id, revision_id, definition_id, role, value_type, boolean_value, creation_time, is_deleted)
                VALUES ({Guid.NewGuid()}, {account.Id}, {revision.Id}, {definition}, 1, 4, false, now(), false)
                """);
            var duplicate = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO journal_experience_observations
                    (id, account_id, revision_id, definition_id, role, value_type, boolean_value, creation_time, is_deleted)
                VALUES ({Guid.NewGuid()}, {account.Id}, {revision.Id}, {definition}, 1, 4, false, now(), false)
                """));
            duplicate.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
            duplicate.ConstraintName.Should().Be("ux_journal_observation_null_row");
            await transaction.RollbackAsync();
            return true;
        });
    }

    [Fact]
    public async Task DataBearingMigrationDownRefusesAndRetainsCoreAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var saved = await fixture.MutateAsync(Create(Draft(Guid.NewGuid())), fixture.Token(account.IdentitySubject));
        var refusal = await Assert.ThrowsAsync<PostgresException>(() => fixture.MigrateAsync(JournalHostFixture.CurrentMigration));
        refusal.SqlState.Should().Be(PostgresErrorCodes.RaiseException);
        (await fixture.ReadAsync(db => db.Database.GetAppliedMigrationsAsync())).Should().Contain(JournalHostFixture.CurrentJournalMigration);
        (await fixture.ReadAsync(db => db.ExperienceEntries.SingleAsync(row => row.Id == saved.EntryId))).CurrentRevision.Should().Be(1);
    }

    private static MutateJournalCommand Create(JournalDraft draft) => new(new CustomerAccountIdentity("ignored", "ignored"),
        Guid.NewGuid(), ExperienceMutationOperation.Create, Guid.NewGuid(), null, null, draft);

    private static JournalDraft Draft(Guid? product) => new(new JournalTargetInput(product, null, null,
        product.HasValue ? null : "Unknown", null, product.HasValue ? "Product" : null),
        ExperienceOccurrence.Create(new DateOnly(2026, 10, 10), ExperienceTimePrecision.DateOnly),
        ExperienceContent.Create(null, null, [], []), ExperienceProfileSnapshotEntity.GeneralV1Id);
}
