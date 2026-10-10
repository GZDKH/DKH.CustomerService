## Purpose

Provide an additive typed personal journal transport that preserves accepted owner/core semantics, supports unknown and General entries, and denies unauthorized context, cursor or Product access.

## ADDED Requirements

### Requirement: Independent personal ownership
Every journal RPC SHALL independently resolve canonical ownership from the authenticated issuer/subject and validated personal purpose, recheck current account lifecycle, and apply that owner to entry/history/unknown/receipt predicates. Requests MUST NOT establish ownership with AccountId, CustomerId, public IDs or forged context. Foreign and missing personal resources SHALL return the same NotFound response. Anonymous callers SHALL receive Unauthenticated and service-only or invalid personal-purpose callers PermissionDenied.

#### Scenario: Forged personal authority
- **WHEN** an anonymous, service-only, duplicate-purpose or forged-owner caller invokes any personal operation
- **THEN** access is denied without serving another account's data or consulting a caller-selected account

#### Scenario: Lifecycle changes before replay
- **WHEN** the owner becomes blocked/deleted before repeating an accepted mutation
- **THEN** lifecycle access is denied before the retained receipt grants any personal response

### Requirement: Authenticated creation origin
Creation and Copy SHALL obtain origin from verified forwarding provenance bound to the authenticated personal principal and trusted storefront context. A raw origin field/header, first parsed UUID or browser client identifier MUST NOT establish this authority. Missing, duplicate, conflicting or unverifiable context SHALL fail closed before mutation. Invalid/conflicting declared context SHALL return InvalidArgument, invalid provenance PermissionDenied and an unavailable required producer FailedPrecondition. Creation MUST remain unavailable until the producer is accepted. Owner-safe reads/history/delete SHALL retain owner authorization independently of fresh Catalog visibility.

#### Scenario: Conflicting storefront context
- **WHEN** a creation request supplies missing, duplicate, conflicting or forged origin provenance
- **THEN** the operation fails without an entry, revision, unknown row or receipt

### Requirement: Lossless typed occurrence and values
The service SHALL preserve calendar OccurredDate, DateOnly or LocalTime precision, nullable score, local time/IANA zone and chosen nullable UtcOffsetMinutes. DateOnly MUST have null time/zone/offset; LocalTime MUST retain either valid selected ambiguous-DST offset and exact supported time precision. Numeric observations SHALL use exact finite decimal18,6 string semantics with explicit typed presence; missing, zero and false MUST remain distinct. Invalid precision, DST gaps, score0/6, type/unit/option/row or content bounds SHALL return InvalidArgument without partial persistence. Audit timestamps MUST NOT replace occurrence date.

#### Scenario: Nullable date-only roundtrip
- **WHEN** an owner submits a valid date-only unknown/General entry without score
- **THEN** exact calendar date and null score/time/zone/offset survive the generated request/response roundtrip

#### Scenario: Both DST choices and decimal boundaries
- **WHEN** a valid frozen-profile fixture includes either ambiguous offset and representable decimal boundaries, zero or false
- **THEN** their distinct exact values survive mapping without rounding or timezone reinterpretation

### Requirement: Additive known-field validation
The service SHALL reject malformed known typed fields, unsupported enum values, ambiguous duplicate known singular/oneof fields and invalid target/value selections. It SHALL preserve acceptance of benign additive unknown protobuf fields within bounded wire limits. Unknown fields MUST NOT establish owner, origin, target permissions or semantic mutation identity. Errors MUST NOT reveal private parser payloads.

#### Scenario: Ambiguous known target and future field
- **WHEN** raw wire contains two conflicting known target alternatives or separately a benign future unknown tag
- **THEN** the conflicting known target is rejected before persistence while the benign future tag remains compatible

### Requirement: Unknown and immutable General transport
The service SHALL provide owner-authorized Create/Update/Get/List/History/Copy/Delete and resolved General profile responses over the existing core. General v1 MUST retain its immutable canonical schema/hash and empty observation definitions without an editorial/Catalog prerequisite. Updates/deletions SHALL append immutable revisions atomically, require expected revision and preserve prior payload/snapshot integrity. Ordinary serving MUST deny deleted entries and their private history.

#### Scenario: Unknown lifecycle
- **WHEN** an owner creates, edits, lists, reads history, copies with a confirmed date and deletes an unknown General entry
- **THEN** typed values and idempotent results are preserved, revisions append, and subsequent ordinary current/history serving is denied after deletion

### Requirement: Authenticated bounded cursors
List and history SHALL use opaque authenticated cursors bound to protocol/schema version and resolved account; list additionally binds normalized supported filters and sort keys, history additionally binds EntryId and revision key. Default page size SHALL be25 and maximum100. Cursors SHALL expire within24h and remain valid across ordinary restart/rotation for their retained lifetime. Invalid, oversized, unsigned, tampered, expired or binding-mismatched cursors SHALL uniformly return InvalidArgument before a personal query. Persistent protection configuration MUST be accepted before cursor serving is enabled.

#### Scenario: Cross-account or changed-filter cursor
- **WHEN** a token is tampered with or reused for another account, filter, entry or protocol version
- **THEN** it is rejected before the corresponding store query without a private page

#### Scenario: Restart and same-date pages
- **WHEN** the owner pages same-date entries or history across ordinary instance restart and key rotation
- **THEN** unexpired tokens preserve owner/filter/entry bounds without gaps or duplicate results

### Requirement: Source-bound Copy identity
Copy SHALL select an ordinarily readable owned immutable source revision and an explicitly confirmed occurrence. It MUST NOT inherit publication intent/consent or create public votes. Within the accepted24-hour receipt lifetime, matching retries SHALL bind source entry/revision, chosen occurrence, trusted creation origin and explicit copy options to the original operation result, without re-reading source private content or creating another destination. Cross-method key reuse or changed source/date/options SHALL conflict with Aborted. A fresh copy of deleted/foreign/missing source SHALL return NotFound. Product retention requires authoritative target validation; an owner-confirmed unknown fallback SHALL create only a private unknown target.

#### Scenario: Source changes after completed copy
- **WHEN** a completed Copy is retried after its source has been edited or deleted while the destination remains ordinarily readable
- **THEN** the original destination/revision is returned without a fresh source grant or duplicate creation

#### Scenario: Different source/date under the same key
- **WHEN** the owner reuses a copy/create key with a different source revision, occurrence or options
- **THEN** Aborted is returned without another destination or receipt

### Requirement: Stable committed mutation response
Within the accepted24-hour receipt lifetime, matching Create/Update/Copy retries SHALL return the original saved entry/revision and body from that committed immutable revision, independently of later edits. They SHALL recheck destination owner/lifecycle/deletion before serving a body. A deleted destination SHALL return NotFound without materializing its retained private body; the internal receipt remains unchanged. Matching unexpired Delete retries SHALL replay Empty after lifecycle validation without exposing history. Stale expected revision and differing idempotent request identity SHALL return Aborted with atomic rollback.

#### Scenario: Edit after successful mutation
- **WHEN** an owner retries a completed mutation after a subsequent edit
- **THEN** its original committed revision/body is returned rather than the latest header/body

#### Scenario: Delete before retry
- **WHEN** an owner retries Create/Update/Copy after deleting its destination
- **THEN** NotFound is returned without serving deleted private content or recreating the entry

### Requirement: Fail-closed Product boundary
Product create/map/rebind, Product-retaining Copy and Product-context profile resolution SHALL require the authoritative target adapter and SHALL return FailedPrecondition while it is unavailable. The intermediate stage MUST NOT authorize Product mutations through an internal core producer. Unchanged owned archived-target edits, history and deletion SHALL preserve owner-safe retained semantics without new target grants; target/profile changes MUST use the blocked adapter boundary.

#### Scenario: Valid Product request before adapter admission
- **WHEN** an otherwise valid authenticated Product create/map/rebind/context request arrives before EJ04 adapter availability
- **THEN** FailedPrecondition is returned before a Catalog or journal mutation and no receipt/revision is committed

### Requirement: Legacy and package compatibility
The additive journal service SHALL preserve old ProductCollection services, message numbers/types, legacy double numeric values, nullable date, text7000, separate rating and summary replace/preserve/clear behavior. New contract tags MUST be fresh. Generated owner/client compatibility, real host authorization, approved additive version and exact package publish/MR/main evidence SHALL be required before transport acceptance. Existing RT02 or documentation validation MUST NOT be represented as RT03 execution or package release.

#### Scenario: Old client against additive package
- **WHEN** an old generated ProductCollection client communicates with the future additive owner host
- **THEN** its prior summary/date/double/rating behavior remains unchanged and no dated entry is implicitly created

#### Scenario: Unapproved package candidate
- **WHEN** only the candidate1.11.0 and this documentation exist
- **THEN** no proto/version commit, publish, consumer upgrade or transport acceptance is authorized or claimed
