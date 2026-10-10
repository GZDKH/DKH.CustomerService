## Context

See the proposal and accepted C2. Customer main retains the old collection summary and the accepted canonical account/purpose resolver. The source snapshot and actual PostgreSQL 17.8 metadata agree on twelve current migrations. The new tables are absent. General must exist before EJ-03/EJ-10 and unknown references before EJ-04; no new RPC or Catalog/Media consumer belongs here.

## Goals / Non-Goals

**Goals:** Deliver the complete owner-local C2 domain, additive schema and deterministic transactional producer, with real PostgreSQL and trusted-principal evidence.

**Non-Goals:** A second account service, generic attribute/idempotency/transaction framework, conversion of legacy summaries/doubles, or a public intake/enablement path. Existing Platform and EF mechanisms remain the infrastructure authority.

## Decisions

### Distinct persistence and ownership

Use new entry/revision/unknown/profile/receipt and row-aware observation persistence. Entries are account-wide rather than collection/membership rows. Restricted account FKs and same-owner composite relationships prevent references to another account's unknown/history/receipt data. Product/Release remain logical references; there is no cross-service FK or Catalog write. Entry current revision is a concurrency token, not a fabricated history conversion. Owner/date/id and owner/Product/date/id indexes support later keyset reads without offset/count scans. Do not add a circular immediate FK from the header to its current revision; the tested transaction maintains that relationship.

### Lossless calendar and local time

Persist OccurredDate as PostgreSQL date. Keep local time independent from UTC audit timestamps. Retain local time as its exact invariant time string, so PostgreSQL microsecond time coercion cannot silently alter a .NET TimeOnly tick; validate the format and IANA zone/date/selected minute offset before mutation. DateOnly has all optional fields null. Both ambiguous offsets are valid only when actually supplied; no automatic choice or UTC-midnight default is allowed. Reject Windows-only zone identifiers and nonexistent local times.

### Canonical snapshots, payloads and typed values

Define a versioned canonical UTF-8 JSON serialization with deterministic property/collection order and exact decimal representation. Persist canonical bytes as text with SHA256 and a byte limit, instead of hashing JSONB's reordered representation. General v1 has fixed renderer/version, empty definitions, null Catalog/Category and a deterministic ID/hash. Non-bootstrap snapshots have both logical bindings and frozen definition/type/unit/option/role/label metadata. Reuse/extract the existing typed shape/enum/unit validation without changing the old double path; new decimals reject range or lossy scale conversion. General cannot accept arbitrary undeclared observations. Repeated rows and observations remain bounded and linked to the frozen payload, not a new universal attribute engine.

Ordinary EF writes cannot modify historical revision/observation or snapshot payloads. Stored hashes are verified before returning frozen material. The later purpose-limited purge path is distinct from ordinary edits. Current headers and explicit owner mapping change only by appending a full new revision; old labels and profile semantics remain retained.

### Reuse the shared transaction boundary

Use the installed Platform outbox transactional MediatR behavior already supported by the host's DbContext/outbox wiring. New owner mutation handlers require an active relational transaction and fail closed if the boundary is absent; they do not create a competing generic transaction service. PostgreSQL transaction-scoped advisory locking serializes a receipt key, and a locked/rechecked account lifecycle row prevents deletion/status races. CAS on CurrentRevision and the unique receipt/revision indexes remain independent integrity protections. Persist header/revision/receipt together, and let the existing boundary commit or roll back. Matching replay is checked after canonical owner/lifecycle validation and before fresh target/profile work, preserving timeout retry semantics.

Expire ordinary receipts after 24 hours under the same key lock and transaction; public operation/tombstone semantics remain later stages. No events, private bodies, labels or media URLs enter logs. Missing/foreign IDs share the same owner-safe error. Internal commands take the trusted resolved identity/context rather than a browser AccountId; the actual C1 principal/purpose producer is reused by the test host and later EJ-03 adapter.

## Risks / Trade-offs

- Platform middleware can pass through with incomplete DI: mutation persistence explicitly requires the relational transaction, and a real-host negative fixture proves failure without it.
- PostgreSQL unique-null behavior differs from InMemory: use a separate null-row partial uniqueness constraint and actual PostgreSQL rejection tests.
- CAS/retry timing and account deletion can race: exercise concurrent separate contexts, transaction rollback and locked lifecycle recheck, including receipt replay.
- Canonical hash/decimal or local-time coercion could alter historical meaning: pin serialization/hash fixtures and roundtrip both DST choices, tick precision, null/zero and decimal boundaries.
- New core exists before its public contract and policy gates: keep intake unmapped/disabled; do not infer pilot, deletion-policy or production authentication acceptance from domain tests.

## Migration Plan

Generate the additive EF migration and inspect its SQL. Validate Up on disposable PostgreSQL 17 using the current schema and old summary/RPC fixtures, then query actual FKs/checks/indexes and run new constraint/CAS/retry/owner tests. Test Down only against empty disposable new-core state, including the known General seed; the operational rollback procedure refuses data-bearing teardown. The normal reviewed owner CI/deployment workflow is the authority for applied runtime state when deployment is selected; no manual production DDL or destructive rollback is part of this work. A data-bearing rollback disables new intake/writers and retains history and owner lifecycle paths. Later export/delete/withdraw functionality remains ordered in EJ-20 and is not claimed as implemented here.
