using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using DKH.CustomerService.Application.CustomerAccounts;
using DKH.CustomerService.Application.ExperienceJournal;
using DKH.CustomerService.Domain.Entities.ExperienceJournal;
using DKH.CustomerService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace DKH.CustomerService.Infrastructure.Persistence.Repositories;

public sealed partial class ExperienceJournalStore(AppDbContext db, CustomerJournalAccounts accounts, TimeProvider clock)
    : IExperienceJournalStore
{
    public async Task<ExperienceMutationResult> MutateAsync(MutateJournalCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            return await MutateCoreAsync(command, cancellationToken);
        }
        catch
        {
            db.ClearTrackedChanges();
            throw;
        }
    }

    private async Task<ExperienceMutationResult> MutateCoreAsync(MutateJournalCommand command, CancellationToken cancellationToken)
    {
        ValidateCommand(command);
        if (!db.Database.IsRelational() || db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Journal mutation requires the shared Platform transaction boundary.");
        }

        var accountId = await ResolveLockedOwnerAsync(command.Identity, true, cancellationToken);
        var owner = new CustomerJournalOwner(accountId);
        var lockBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{owner.AccountId:N}:{(int)command.Operation}:{command.IdempotencyKey:N}"));
        var lockKey = BinaryPrimitives.ReadInt64BigEndian(lockBytes);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockKey})", cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        // PostgreSQL audit timestamps retain microseconds. Normalize the
        // producer clock before computing/comparing the exact 24-hour receipt
        // lifetime; local occurrence time keeps its independent seven ticks.
        now = now.AddTicks(-(now.Ticks % 10));
        var requestHash = ExperienceCanonicalJson.Hash(ExperienceCanonicalJson.Serialize(new
        {
            command.Operation,
            OriginStorefrontId = command.Operation == ExperienceMutationOperation.Create ? command.OriginStorefrontId : (Guid?)null,
            command.EntryId,
            command.ExpectedRevision,
            command.Draft,
        }));
        var receipt = await db.ExperienceMutationReceipts.AsNoTracking().SingleOrDefaultAsync(row => row.AccountId == owner.AccountId
            && row.Operation == command.Operation && row.IdempotencyKey == command.IdempotencyKey, cancellationToken);
        if (receipt is not null && receipt.IsReplayable(now))
        {
            return receipt.MatchesRequest(requestHash) ? receipt.OriginalResult : throw new JournalMutationConflictException();
        }

        return await ApplyMutationAsync(command, owner, now, requestHash, receipt, cancellationToken);
    }

    private async Task<Guid> ResolveLockedOwnerAsync(CustomerAccountIdentity identity, bool forWrite, CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational() || db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Journal access requires the shared Platform transaction boundary.");
        }

        var owner = await accounts.ResolveAsync(identity, cancellationToken);
        var locked = forWrite
            ? db.CustomerAccounts.FromSqlInterpolated($"SELECT * FROM customer_accounts WHERE \"Id\" = {owner.AccountId} FOR UPDATE")
            : db.CustomerAccounts.FromSqlInterpolated($"SELECT * FROM customer_accounts WHERE \"Id\" = {owner.AccountId} FOR SHARE");
        var account = await locked
            .IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw new JournalResourceNotFoundException();
        CustomerJournalAccounts.RequireActiveOwner(account);
        // Recheck current primary/verified aliases after acquiring the lifecycle lock.
        if ((await accounts.ResolveAsync(identity, cancellationToken)).AccountId != owner.AccountId)
        {
            throw new JournalResourceNotFoundException();
        }

        return owner.AccountId;
    }

    private async Task<ExperienceMutationResult> ApplyMutationAsync(MutateJournalCommand command, CustomerJournalOwner owner,
        DateTime now, string requestHash, ExperienceMutationReceiptEntity? receipt, CancellationToken cancellationToken)
    {
        if (receipt is not null)
        {
            if (receipt.ExpiresAtUtc > now)
            {
                throw new InvalidOperationException("Mutation receipt clock is outside its retained lifetime.");
            }

            // Only the expired key under the same lock is removed; no retained history is affected.
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM journal_experience_mutation_receipts WHERE account_id = {owner.AccountId} AND operation = {(int)command.Operation} AND idempotency_key = {command.IdempotencyKey} AND expires_at_utc <= {now}", cancellationToken);
        }

        ExperienceEntryEntity entry;
        if (command.Operation == ExperienceMutationOperation.Create)
        {
            var resolved = await ResolveDraftAsync(owner.AccountId, command.Draft!, cancellationToken);
            entry = ExperienceEntryEntity.Create(owner.AccountId, command.OriginStorefrontId, resolved.Target,
                command.Draft!.Occurrence, command.Draft.Content, resolved.Profile, now,
                command.Draft.CatalogId, command.Draft.CategoryId, resolved.Unknown);
            db.ExperienceEntries.Add(entry);
        }
        else
        {
            entry = await db.ExperienceEntries.SingleOrDefaultAsync(row => row.Id == command.EntryId && row.AccountId == owner.AccountId,
                cancellationToken) ?? throw new JournalResourceNotFoundException();
            if (entry.CurrentRevision != command.ExpectedRevision)
            {
                throw new JournalMutationConflictException();
            }

            var current = await db.ExperienceRevisions.SingleAsync(row => row.EntryId == entry.Id && row.AccountId == owner.AccountId
                && row.Revision == entry.CurrentRevision, cancellationToken);
            if (command.Operation == ExperienceMutationOperation.Delete)
            {
                entry.AppendDeletion(command.ExpectedRevision!.Value, current, now);
            }
            else
            {
                var resolved = await ResolveDraftAsync(owner.AccountId, command.Draft!, cancellationToken);
                if (command.Operation == ExperienceMutationOperation.Update
                    && (resolved.Target.ProductId != entry.ProductId || resolved.Target.ReleaseId != entry.ReleaseId
                        || resolved.Target.UnknownReferenceId != entry.UnknownReferenceId
                        || resolved.Target.RetainedLabel != entry.RetainedTargetLabel || resolved.Profile.Id != current.ProfileSnapshotId))
                {
                    throw new ArgumentException("Target or profile rebinding requires an explicit owner mapping operation.");
                }

                entry.Append(command.ExpectedRevision!.Value, resolved.Target, command.Draft!.Occurrence, command.Draft.Content,
                    resolved.Profile, now, command.Draft.CatalogId, command.Draft.CategoryId, resolved.Unknown);
            }

            db.ExperienceRevisions.Add(entry.Revisions.Last());
        }

        var result = new ExperienceMutationResult(entry.Id, entry.CurrentRevision);
        db.ExperienceMutationReceipts.Add(ExperienceMutationReceiptEntity.Create(owner.AccountId, command.Operation,
            command.IdempotencyKey, requestHash, result.EntryId, result.Revision, now));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ClearTrackedChanges();
            throw new JournalMutationConflictException();
        }

        return result;
    }

    private async Task<ResolvedDraft> ResolveDraftAsync(Guid accountId, JournalDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft.Target);
        var profile = await db.ExperienceProfileSnapshots.SingleOrDefaultAsync(row => row.Id == draft.ProfileSnapshotId,
            cancellationToken) ?? throw new JournalResourceNotFoundException();
        ExperienceUnknownReferenceEntity? unknown = null;
        var input = draft.Target;
        var supplied = (input.ProductId.HasValue ? 1 : 0) + (input.UnknownReferenceId.HasValue ? 1 : 0) + (input.UnknownLabel is not null ? 1 : 0);
        if (supplied != 1 || (input.ReleaseId.HasValue && !input.ProductId.HasValue)
            || (input.ProducerLabel is not null && input.UnknownLabel is null))
        {
            throw new ArgumentException("Select exactly one Product or private unknown target.");
        }

        ExperienceTarget target;
        if (input.ProductId.HasValue)
        {
            target = ExperienceTarget.Create(input.ProductId, null, input.ReleaseId, input.RetainedProductLabel!);
        }
        else
        {
            if (input.UnknownReferenceId.HasValue)
            {
                unknown = await db.ExperienceUnknownReferences.SingleOrDefaultAsync(row => row.Id == input.UnknownReferenceId
                    && row.AccountId == accountId, cancellationToken) ?? throw new JournalResourceNotFoundException();
            }
            else
            {
                unknown = ExperienceUnknownReferenceEntity.Create(accountId, input.UnknownLabel!, input.ProducerLabel);
                db.ExperienceUnknownReferences.Add(unknown);
            }

            target = ExperienceTarget.Create(null, unknown.Id, null, unknown.OwnerLabel);
        }

        return new ResolvedDraft(target, profile, unknown);
    }

    private static void ValidateCommand(MutateJournalCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!Enum.IsDefined(command.Operation) || command.IdempotencyKey == Guid.Empty
            || (command.Operation == ExperienceMutationOperation.Create && (command.EntryId.HasValue || command.ExpectedRevision.HasValue || command.OriginStorefrontId == Guid.Empty))
            || (command.Operation != ExperienceMutationOperation.Create && (command.EntryId is null || command.EntryId == Guid.Empty || command.ExpectedRevision is null or < 1))
            || (command.Operation == ExperienceMutationOperation.Delete ? command.Draft is not null : command.Draft is null))
        {
            throw new ArgumentException("A valid owner mutation, idempotency key and expected revision are required.");
        }
    }

    private sealed record ResolvedDraft(ExperienceTarget Target, ExperienceProfileSnapshotEntity Profile, ExperienceUnknownReferenceEntity? Unknown);
}
