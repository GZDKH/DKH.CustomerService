## Purpose

Preserve several explicitly dated private experiences and their immutable history under the canonical account owner, while keeping existing collection summaries compatible.

## ADDED Requirements

### Requirement: Repeated account-owned dated entries
The owner SHALL persist separate entries for repeated experiences of the same Product. Each entry MUST have a canonical account owner, trusted creation storefront, explicit calendar date, positive current revision and UTC creation/update metadata. Exactly one Product or owned private unknown reference MUST be selected; a Release MUST require a Product. Product, Release and private pack/lot context MUST remain distinct.

#### Scenario: Three sessions for one Product
- **WHEN** an active canonical owner creates three experiences for the same Product with three distinct mutation keys
- **THEN** three separate entries and initial revisions are retained, without changing a collection summary or adding public votes

#### Scenario: Invalid or foreign target
- **WHEN** a mutation supplies both target kinds, neither target kind, a Release without Product, or a foreign unknown reference
- **THEN** invalid target shape is rejected and a foreign/missing private reference has the same NotFound result, without persisted partial rows

### Requirement: Explicit date and lossless selected local time
DateOnly experiences MUST retain the chosen calendar date with null time, timezone and UTC offset. LocalTime experiences MUST retain valid local time, valid IANA timezone and a chosen offset in minutes compatible with that zone/date/time. Nonexistent local time or incompatible offset MUST be rejected. Creation metadata MUST NOT substitute for the experience date.

#### Scenario: Date-only roundtrip
- **WHEN** an owner creates a date-only experience without score or local time
- **THEN** the exact calendar date roundtrips and time, timezone, offset and score remain null

#### Scenario: Both ambiguous DST choices
- **WHEN** an owner chooses either valid offset for an ambiguous local time
- **THEN** both choices can be persisted and roundtrip without changing the chosen date, local time or offset

#### Scenario: Invalid timezone or DST gap
- **WHEN** local time falls in a DST gap, the timezone is not valid IANA, an offset does not match, or DateOnly includes time fields
- **THEN** the mutation is rejected without a revision or receipt

### Requirement: Bounded private content and exact numeric semantics
New private notes MUST be bounded to 16,000 characters and the complete canonical personal payload to 128 KiB UTF-8 excluding photos. Tags MUST be unique, at most 50 and at most 32 characters each. Repeated rows MUST be bounded to 12 and observations to 200. Optional score MUST be an integer from 1 through 5. New numeric observations MUST retain exact finite decimal precision (18,6); missing MUST remain distinct from zero or false. Legacy doubles MUST NOT be silently converted or relabeled.

#### Scenario: Optional values and decimal boundary
- **WHEN** a valid bounded entry contains a null score, zero numeric observation, false Boolean observation or representable decimal boundary
- **THEN** the distinct values survive the revision and persistence roundtrip

#### Scenario: Limit or numeric violation
- **WHEN** score is 0 or 6, content exceeds any bound, numeric value is non-finite/out of range or requires lossy precision reduction, or a typed value is absent/mismatched
- **THEN** the complete mutation is rejected without partial persistence

### Requirement: Frozen profile semantics and bootstrap General
The owner SHALL supply an immutable General v1 snapshot with empty observation definitions and reproducible canonical schema hash. Every other snapshot MUST bind both Catalog and Category and freeze resolved definitions, types, units, options, roles and labels. Observations MUST validate against their frozen snapshot, including category applicability, permitted conversion and options. Catalog facts and recommendation defaults MUST remain read-only context.

#### Scenario: Stable General bootstrap
- **WHEN** General is obtained repeatedly across contexts or the process restarts
- **THEN** its canonical payload, renderer version and schema hash are unchanged and it requires no category/profile producer

#### Scenario: Frozen observation validation
- **WHEN** an observation references an unknown definition, wrong value type, unsupported unit/option, wrong bound/category, or read-only context field
- **THEN** it is rejected without rewriting the snapshot or Catalog

### Requirement: Immutable revisions and row-aware observations
Each accepted edit, mapping or deletion MUST append the next unique revision and retain historical canonical payload, hash, snapshot binding and creation metadata. Observations MUST be unique by revision, definition, role and repeated-row identity, including null row. Only purpose-limited account purge MAY erase history; ordinary mutation MUST NOT rewrite it.

#### Scenario: Edit and mapping preserve history
- **WHEN** an owner edits an entry or explicitly maps its unknown target
- **THEN** the new revision and current header change atomically, while the previous payload, private label and frozen semantics are unchanged

#### Scenario: Duplicate or historical rewrite
- **WHEN** duplicate observations share the same role/row, including null row, or an ordinary write attempts to modify a historical payload/snapshot
- **THEN** the write is rejected and the saved history remains unchanged

### Requirement: Atomic receipts and conflict-safe mutations
Create and Update MUST atomically persist the entry/header, complete new revision and ordinary mutation receipt. Receipts MUST be uniquely scoped to account, operation and idempotency key and remain replayable for 24 hours. Matching canonical request identity MUST return the original stable result; differing content MUST return conflict. Update, Delete and Map MUST require expected revision. A concurrent CAS loser MUST receive conflict without changing the saved version or mutating the caller's draft.

#### Scenario: Timeout retry and body mismatch
- **WHEN** a committed write is retried with its matching key and body, or the same key is reused with different content
- **THEN** matching replay returns the original entry/revision result and differing content returns Aborted/HTTP409 without another entry or revision

#### Scenario: Simultaneous CAS and receipt race
- **WHEN** two writes contend on an expected revision or the same receipt key
- **THEN** only one new revision/result is committed; a matching receipt retry replays it, a stale update conflicts, and rollback leaves no ghost rows

### Requirement: Owner and lifecycle isolation
All mutations and entry, history, unknown and receipt access MUST use the independently resolved canonical account and current account lifecycle status. Browser identifiers MUST NOT establish ownership. Service credentials alone MUST NOT grant personal access. Foreign and missing personal resources MUST have the same NotFound response; blocked/deleted accounts MUST be denied, including cached receipt replay.

#### Scenario: Different owner and lifecycle change
- **WHEN** a second account attempts access or mutation, or the owning account becomes blocked/deleted
- **THEN** private entry/history/unknown/receipt access is denied without disclosing another account's data

### Requirement: Additive compatibility and reversible intake
The dated core MUST use new owner-local tables with restricted account/local ownership relationships and owner/date/id plus Product access indexes. Existing ProductExperience MUST retain replace/preserve/clear behavior, 7,000-character text, nullable date, double wire/storage and separate rating. No automatic historical backfill SHALL occur. Rollback with user data MUST retain tables, owner-safe reads and lifecycle access; destructive Down MUST be limited to an empty disposable database.

#### Scenario: Migration and old client
- **WHEN** the additive migration runs on disposable PostgreSQL containing the existing schema and old-client fixtures
- **THEN** new constraints/indexes and null uniqueness hold while existing summaries and old clients retain their behavior without invented sessions

#### Scenario: Data-bearing rollback
- **WHEN** dated user data exists and the new intake is rolled back
- **THEN** tables/history remain intact, new writes stop, and export/deletion/owner reads remain available through their accepted owner paths
