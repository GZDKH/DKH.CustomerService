## Why

The existing collection experience is one replaceable summary; it cannot represent several dated personal sessions or immutable edit history. The accepted Revision 2 EJ-02 scope adds that private core alongside the summary after EJ-00/EJ-01 acceptance, without changing old clients or enabling a pilot.

## Discovery decision and evidence

`extend`: reuse `CustomerJournalAccounts`, the typed `ProductExperienceObservationEntity` validation, existing Platform/EF persistence and accepted principal-purpose boundary. Customer main `16d4c0ecbb3eb74f3da577c38bfa6f21ccdc6854` has no dated entry/revision/receipt implementation; its snapshot, handlers, protobuf and tests describe the replaceable collection summary. Gateway main `bcae25ec2fd716872b3be79d1dbff0ff52eda34d` preserves that nullable-date/double-wire consumer. Platform main `cd1ec567e2581a20b31c9df1e16b5e4b11d39e08` and installed Messaging.MediatR 1.2.0 supply the existing transaction mechanisms; a new generic transaction/idempotency framework is unnecessary.

Fresh deterministic core searches covered all three repositories with 29 bounded matches and no truncation. Customer has no competing open MR; inspected related branches contain accepted EJ-00/EJ-01 work or unrelated ancestral evidence, not a second new-core writer. Metadata-only production PostgreSQL 17.8 inspection at 2026-10-09T07:09:05Z confirms the active-summary index migration and legacy `double precision` storage, and no proposed dated core tables. Actual local package restore succeeded for all seven projects. Detailed receipt: AgentRules commit `9ac1e97a2655ba5f33b3b74843e89cb6f9f467ae`, `plans/product-experience-journal/evidence/ej02-discovery.json` and its schema/package receipts.

Accepted inputs are the published Revision 2 `CONTRACTS.en.md` C1/C2 and `publication/task-bodies/EJ-02.md`, together with the existing `experience-journal-owner` and `private-structured-product-experience` owner requirements. Execution is original gate `dkh-rzx95f`, owner child `dkh-rzx95f.1` ([mirror #12196](https://gitlab.xnata.com/gzdkh/agents/DKH.Beads/-/issues/12196)), [Customer issue #9](https://gitlab.xnata.com/gzdkh/services/DKH.CustomerService/-/issues/9), branch `feat/9-journal-dated-core-01a117e3` in the leased Customer worktree. The 24 stages, 39 gates and 87 original owner scopes are retained.

## What Changes

- Add account-owned entries, immutable revisions, private unknown references, frozen profile snapshots and 24-hour mutation receipts in owner-local tables. Reuse typed validation with decimal/row-aware additions, leaving the old double contract intact.
- Produce canonical General v1 with empty observation definitions, plus validation/storage of frozen typed snapshot semantics for the later profile producer.
- Persist explicit calendar dates and optional valid IANA local time/offset, nullable scores, bounded notes/tags/rows/observations and private pack/lot labels. Never manufacture timestamps from date-only values or legacy summaries.
- Persist create/update/revision/receipt atomically; enforce retry-body identity, expected-revision CAS, same-owner local relationships and lifecycle-safe owner predicates, including history. Mapping and deletion retain historical payloads and use expected revision.
- Add an EF-generated additive migration, real PostgreSQL constraint/transaction/race and compatibility evidence, and rollback procedures that retain user data. New intake remains disabled until later contract/UI/pilot gates.

## Capabilities

### New Capabilities

- `private-dated-journal-core`: dated owner-local persistence, immutable General/profile and revision semantics, bounded typed observations, mutation receipt/CAS behavior and additive compatibility.

### Modified Capabilities

None. The accepted identity/purpose resolver and existing collection summary requirements are retained.

## Impact

Customer Domain, Application and Infrastructure persistence and tests only, with owner documentation. No breaking schema or legacy RPC changes. No new public gRPC service/client package, BFF/OpenAPI/UI, Catalog writes or target resolver, Media/publication logic, enabled photos/infusions/category UI, backfill from ratings/reviews, or pilot claims. EJ-03/EJ-04/EJ-10 and the P02/P06 policy gates remain separately ordered. Native Keycloak upgrade is a separate incomplete task; this change does not bypass its backup or foreign-session authority gates.
