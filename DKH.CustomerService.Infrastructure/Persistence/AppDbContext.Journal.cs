using DKH.CustomerService.Domain.Entities.ExperienceJournal;
using Microsoft.EntityFrameworkCore;

namespace DKH.CustomerService.Infrastructure.Persistence;

public partial class AppDbContext
{
    protected override void ApplyAuditInfoRules()
    {
        ValidateJournalWrites();
        base.ApplyAuditInfoRules();
    }

    private void ValidateJournalWrites()
    {
        ChangeTracker.DetectChanges();
        var writes = ChangeTracker.Entries().Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
            && entry.Entity is ExperienceEntryEntity or ExperienceRevisionEntity or ExperienceRevisionObservationEntity
                or ExperienceProfileSnapshotEntity or ExperienceUnknownReferenceEntity or ExperienceMutationReceiptEntity).ToArray();
        if (writes.Length == 0)
        {
            return;
        }

        if (!Database.IsRelational() || Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Journal writes require the shared relational transaction boundary.");
        }

        if (writes.Any(entry => entry.State != EntityState.Added && entry.Entity is not ExperienceEntryEntity))
        {
            throw new InvalidOperationException("Ordinary writes cannot rewrite or remove retained journal rows.");
        }

        var revisions = writes.Where(entry => entry.Entity is ExperienceRevisionEntity)
            .Select(entry => (ExperienceRevisionEntity)entry.Entity).ToArray();
        var headers = writes.Where(entry => entry.Entity is ExperienceEntryEntity).ToArray();
        foreach (var header in headers)
        {
            var entity = (ExperienceEntryEntity)header.Entity;
            if (header.State == EntityState.Deleted)
            {
                throw new InvalidOperationException("Journal deletion must append a retained tombstone revision.");
            }

            var expected = header.State == EntityState.Added ? 1 : (long)header.OriginalValues[nameof(entity.CurrentRevision)]! + 1;
            if (entity.CurrentRevision != expected || revisions.Count(revision => revision.EntryId == entity.Id
                    && revision.AccountId == entity.AccountId && revision.Revision == entity.CurrentRevision
                    && revision.IsDeletion == entity.IsDeleted) != 1
                || (header.State == EntityState.Modified && (header.Property(nameof(entity.AccountId)).IsModified
                    || header.Property(nameof(entity.OriginStorefrontId)).IsModified || header.Property(nameof(entity.CreatedAtUtc)).IsModified)))
            {
                throw new InvalidOperationException("A journal header change requires exactly one matching next revision.");
            }
        }

        foreach (var revision in revisions)
        {
            if (!headers.Any(header => ((ExperienceEntryEntity)header.Entity).Id == revision.EntryId
                && ((ExperienceEntryEntity)header.Entity).AccountId == revision.AccountId))
            {
                throw new InvalidOperationException("A new journal revision requires its matching header change.");
            }

            revision.ReadCanonicalPayload();
        }

        foreach (var observation in writes.Where(entry => entry.Entity is ExperienceRevisionObservationEntity)
                     .Select(entry => (ExperienceRevisionObservationEntity)entry.Entity))
        {
            if (!revisions.Any(revision => revision.Id == observation.RevisionId && revision.AccountId == observation.AccountId))
            {
                throw new InvalidOperationException("A new observation requires a matching new owned revision.");
            }
        }

        foreach (var receipt in writes.Where(entry => entry.Entity is ExperienceMutationReceiptEntity)
                     .Select(entry => (ExperienceMutationReceiptEntity)entry.Entity))
        {
            if (!revisions.Any(revision => revision.EntryId == receipt.EntryId && revision.AccountId == receipt.AccountId
                && revision.Revision == receipt.ResultRevision))
            {
                throw new InvalidOperationException("A new mutation receipt requires a matching new owned revision.");
            }
        }
    }
}
