using DKH.CustomerService.Domain.Entities.CustomerAddress;
using DKH.CustomerService.Domain.Entities.DeliveryContact;
using DKH.CustomerService.IntegrationTests.Integration.Journal;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace DKH.CustomerService.IntegrationTests.Integration.DeliveryContacts;

// A separate instance of the existing owned fixture starts with empty contact
// history. No new container/provider lifecycle or production profile is added.
[Trait("Category", "Integration")]
public sealed class DeliveryContactDownConcurrencyTests(JournalHostFixture fixture) : IClassFixture<JournalHostFixture>
{
    [Fact]
    public async Task WriterCommittedWhileDownWaitsIsRetainedAndDowngradeRefusesBeforeDdlAsync()
    {
        await fixture.ReadAsync(async db =>
        {
            await db.Database.MigrateAsync();
            return true;
        });
        var writerSaved = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWriter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var downPid = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var writer = fixture.ReadAsync(async db =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            // Technical history row tests migration retention, not an invented
            // positive recipient authority or a public owner binding.
            var address = CustomerAddressEntity.Create(Guid.NewGuid(), "Migration fixture", "GB", "London");
            var row = DeliveryContactRevisionEntity.Observe(Guid.NewGuid(), Guid.NewGuid(), address, 1,
                Guid.NewGuid(), DateTimeOffset.UtcNow, new string('a', 64));
            db.Set<DeliveryContactRevisionEntity>().Add(row);
            await db.SaveChangesAsync();
            writerSaved.SetResult(row.Id);
            await releaseWriter.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await transaction.CommitAsync();
            return true;
        });
        Task<Exception?>? down = null;
        try
        {
            var retainedId = await writerSaved.Task.WaitAsync(TimeSpan.FromSeconds(15));
            down = fixture.ReadAsync<Exception?>(async db =>
            {
                await db.Database.OpenConnectionAsync();
                downPid.SetResult(await db.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync());
                return await Record.ExceptionAsync(() => db.GetService<IMigrator>().MigrateAsync(JournalHostFixture.CurrentJournalMigration));
            });
            var pid = await downPid.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var waiting = await fixture.ReadAsync(async db =>
            {
                for (var attempt = 0; attempt < 200; attempt++)
                {
                    var blocked = await db.Database.SqlQuery<bool>($"SELECT EXISTS (SELECT 1 FROM pg_locks WHERE pid = {pid} AND NOT granted AND mode = 'AccessExclusiveLock' AND relation = 'public.delivery_contact_revisions'::regclass) AS \"Value\"").SingleAsync();
                    if (blocked)
                    {
                        return true;
                    }

                    if (down.IsCompleted)
                    {
                        return false;
                    }

                    await Task.Delay(25);
                }

                return false;
            });
            waiting.Should().BeTrue("downgrade must serialize with the actual uncommitted writer");
            releaseWriter.SetResult();
            await writer.WaitAsync(TimeSpan.FromSeconds(15));
            var failure = await down.WaitAsync(TimeSpan.FromSeconds(30));
            var postgres = failure as PostgresException ?? (failure is InvalidOperationException { InnerException: PostgresException inner } ? inner : null);
            postgres.Should().NotBeNull("only the provider or its immediate EF execution wrapper is a valid refusal");
            postgres!.SqlState.Should().Be("55000");
            postgres.MessageText.Should().Be("Delivery contact authority history must be retained before downgrade.");
            await fixture.ReadAsync(async db =>
            {
                (await db.Set<DeliveryContactRevisionEntity>().CountAsync(row => row.Id == retainedId)).Should().Be(1);
                (await db.Database.GetAppliedMigrationsAsync()).Should().Contain("20261015121242_AddDeliveryContactAuthority");
                return true;
            });
        }
        finally
        {
            releaseWriter.TrySetResult();
            await writer.WaitAsync(TimeSpan.FromSeconds(35));
            if (down is not null)
            {
                await down.WaitAsync(TimeSpan.FromSeconds(35));
            }
        }
    }
}
