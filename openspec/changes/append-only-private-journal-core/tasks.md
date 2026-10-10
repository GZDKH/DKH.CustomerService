## 1. Domain and frozen payloads

- [x] 1.1 Add the complete C2 entry/revision/unknown/profile/receipt model with shared legacy typed validation reuse; prove target XOR, release dependency, score/null, note/tag/row/value bounds and exact decimal semantics.
- [x] 1.2 Add versioned canonical payload/schema hashing and immutable General v1; prove deterministic hash, frozen definition/unit/option/category validation and ordinary historical-write refusal.
- [x] 1.3 Add lossless date/local-time/IANA/selected-offset validation; prove DateOnly null fields, both ambiguous DST offsets, exact local-time roundtrip and gap/invalid-zone rejection.

## 2. Additive persistence and mutation producer

- [x] 2.1 Configure owner/local restricted FKs, lifecycle filters, revision/receipt uniqueness, null-row partial uniqueness, exact decimal checks and owner/date/Product indexes; inspect the EF-generated migration and SQL without legacy backfill.
- [x] 2.2 Implement owner mutation handlers using the existing Platform transaction behavior, locked lifecycle recheck, receipt-key serialization, atomic revision/header/receipt persistence, matching replay and CAS/body-conflict behavior; fail closed without the shared transaction boundary.
- [x] 2.3 Implement owner-safe current/history/unknown access and expected-revision edit/map/delete semantics with retained historical labels/payloads; prove no browser-authoritative account input or service-purpose bypass.

## 3. Actual PostgreSQL and compatibility evidence

- [x] 3.1 Run disposable PostgreSQL 17 Up and query actual constraints/indexes/types; test real FK/check/null behavior, immutable snapshots, exact numeric/time roundtrips and legacy old-client/summary fixtures.
- [x] 3.2 Run independent-context receipt/CAS/lifecycle races and transaction-failure rollback tests, plus actual JWT/host negative-owner/service-purpose cases; retain named RT02 evidence without private contents in logs.
- [x] 3.3 Verify empty-disposable Down and data-bearing rollback refusal/retention procedure, cold intake, no fabricated sessions and no new public contract, catalog/media/UI or pilot claim.

## 4. Delivery and exact acceptance

- [ ] 4.1 Update owner schema/behavior/runbook documentation, pass the repository's required Release/build/format/security/strict-OpenSpec gates, publish one coherent owner MR and obtain independent current-head review.
- [ ] 4.2 Verify authorized merge, authoritative main/deploy evidence as applicable and own cleanup; reconcile only the registered EJ-02.01 step and child, then separately verify the original EJ-02 parent gate before closure.
