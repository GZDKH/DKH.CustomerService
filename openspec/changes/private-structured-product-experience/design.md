## Context

See proposal.md. The current collection item is a single row with `Notes` and a 1–5 rating, and the existing gRPC contract already carries additive model fields. Customer data exchange currently exports addresses and wishlist items; product collection rows are not yet part of that profile projection.

## Goals / Non-Goals

**Goals:**

- Keep personal observations separate from editorial product attributes and public reviews.
- Reuse stable catalog definition IDs as references without creating definitions during a user save.
- Make scalar type, unit, role, and presence explicit so a false or zero value is not lost.
- Preserve owner-only access through the existing customer binding and make updates idempotent.

**Non-Goals:**

- A tea-specific service, runtime registry, TeaDB integration, sensory scale migration, or automatic publication.
- Guessing definition existence or silently creating catalog definitions.
- Replacing ReviewService's public 1–5 review contract.

## Decisions

1. **One optional experience aggregate per collection item.** A one-to-one child aggregate keeps the current collection identity and leaves repeated timestamped sessions for a later phase. An independent session table was rejected for this slice because it would change UX and history semantics beyond the first private note.
2. **Normalized child rows instead of JSON.** Experience metadata is a parent row, tags are child rows, and observations are child rows with nullable scalar columns plus a value-type discriminator. JSON in `Notes` or a JSONB blob was rejected because it prevents canonical validation, stable section rendering, and safe export.
3. **Definition references are opaque stable IDs.** The service validates shape and one-of values; catalog ownership remains in ProductCatalogService. Saves with unknown definitions are rejected by the caller's canonical definition resolver in the next integration slice rather than creating definitions here.
4. **Message presence controls update semantics.** An absent experience message preserves the draft; a present empty message clears it. Observation rows are replaced atomically for a present message, making retry deterministic.
5. **Additive v1 protobuf and minor contracts bump.** Existing clients ignore new fields; no field numbers are reused.

## Risks / Trade-offs

- [Unknown definition IDs] → keep them out of public output and require the catalog resolver before the storefront enables a control.
- [Large observation payload] → enforce bounded rows, text, unit and tag lengths in domain/application validation.
- [Concurrent updates] → use the existing audited row timestamps and return a conflict when the expected modification token is supplied in the follow-up gateway slice.
- [Data exchange privacy] → export the experience only within an authenticated owner profile export; never include it in public catalog or review projections.

## Migration Plan

1. Apply the additive contracts and EF migration.
2. Deploy CustomerService; existing collection rows receive no experience.
3. Enable the storefront form only after catalog definition resolution is wired.
4. Rollback is application-compatible: old consumers ignore the new fields; removing the migration requires a controlled data decision and is not part of this change.
