# Product Experience Journal — EJ-00 entry discovery

Observed: 2026-10-08, Asia/Shanghai. CustomerService source: `c176ebea92c5500e2cc31646dc1d38ee440408c8`.

Scope: read-only discovery of the accepted Revision 2 plan. No application, schema, configuration, deployment, permission or policy change. Source discovery supports the next bounded ownership delta; it does not prove that a diary exists or that a pilot can be enabled.

Program: [CustomerService #7](https://gitlab.xnata.com/gzdkh/services/DKH.CustomerService/-/issues/7). Phase grouping: [CustomerService #8](https://gitlab.xnata.com/gzdkh/services/DKH.CustomerService/-/issues/8). Exact stage Bead: `dkh-4mrzn0`, [task #12180](https://gitlab.xnata.com/gzdkh/agents/DKH.Beads/-/issues/12180). Atomic owner scope: `EJ-00.01-Customer`, the same no-code coordinator discovery scope as this single-owner stage/gate task.

Accepted package: [plan](https://gitlab.xnata.com/gzdkh/agents/DKH.AgentRules/-/blob/f01aad0eae8cc0e3517f70b08c2b7abf699c98f4/plans/product-experience-journal/2026-10-08-product-experience-journal.md), [worker plan](https://gitlab.xnata.com/gzdkh/agents/DKH.AgentRules/-/blob/f01aad0eae8cc0e3517f70b08c2b7abf699c98f4/plans/product-experience-journal/package/readiness/WORKER-PLAN.en.md) and [contracts](https://gitlab.xnata.com/gzdkh/agents/DKH.AgentRules/-/blob/f01aad0eae8cc0e3517f70b08c2b7abf699c98f4/plans/product-experience-journal/package/readiness/CONTRACTS.en.md). Publication MR !1506 is open; its merge/main acceptance is not claimed. Preserve all 24 stages, 39 partial gates and 87 owner scopes. This is separate from unified-commerce and pipeline recovery.

## Fresh refs and concurrent ownership

All 12 `origin/main` fetches succeeded. [Active MR receipt](active-mrs.json) records exact source heads/URLs for the current open MRs. ProductCatalogService !527 is existing unified-commerce discovery; coordinate its producer before a Catalog delta. Other observed active MRs concern dependencies. No separate open diary implementation MR was observed at this audit time.

[Related worktrees](related-worktrees.json) records 8 branch-name alias matches. The 7 foreign legacy experience/specification branches have merged MR evidence; some original branch heads are not main ancestors because merge modes rewrite commits. Do not treat that as an unmerged feature or reuse/clean those worktrees. This session owns only its new CustomerService audit worktree and the AgentRules publication worktree.

| Repository | Fresh main SHA |
|---|---|
| `services/DKH.CustomerService` | `c176ebea92c5500e2cc31646dc1d38ee440408c8` |
| `services/DKH.ReviewService` | `facb8a9f7b74dd505fdbf32bdd121b5ab8ab37d7` |
| `services/DKH.ProductCatalogService` | `515bc4d6175de10d2ee056d6281aa21f237b58ff` |
| `services/DKH.StorefrontService` | `db37f8b01f024d78333f51fbabc5a1f4d93e2ee3` |
| `services/DKH.MediaService` | `cf7df147c2ad8b74d03855889cbb53b7b5b5a8b8` |
| `gateways/DKH.StorefrontGateway` | `3f1ed9a58a2495cfa3be84ab8b9e9e9d3f4836ea` |
| `gateways/DKH.AdminGateway` | `02f51ecbbb62a61745228d8ad7106381ab735097` |
| `ui/DKH.Storefront.Web.UI` | `26e40c9a3015ada32bc13e46d401637a67c1deb8` |
| `ui/DKH.Admin.Web.UI` | `d74c02fd1da51b9de6201982e00f4e4c3cb7fdda` |
| `libraries/DKH.Platform` | `605a7ab7ad5a9ce08ebff2f5840154fba15f2a12` |
| `services/DKH.ReferenceService` | `a0c2e59d5d5c111ece1a467f926c9452b5fe56a6` |
| `libraries/DKH.Architecture` | `8c8545d03e11e064bf71056ebc9a70f0fb8006a1` |

## Existing implementation and explicit decisions

| Boundary | Evidence | Decision and retained scope |
|---|---|---|
| Customer canonical owner | `CustomerAccountGrpcService.ResolveIdentity`, `CustomerAccountHandlerSupport.RequireAccountAsync` | Extend the validated configured issuer/string-subject path and account status guards for new personal RPCs. Do not derive CustomerProfile UUID from `sub`, accept browser AccountId, or merge by email. |
| Legacy collection summary | `ProductExperienceEntity`, `ProductExperienceConfiguration`, `ProductCollectionGrpcService`, existing private-structured-product-experience OpenSpec | Extend alongside the unique one-per-CollectionItem summary. Keep nullable date, 7000-character text, omission versus clear and separate collection Rating. No fabricated historical sessions. |
| Legacy bridge/deletion | `CustomerAccountCommandHandlers`, `DeleteCustomerAccountDataCommandHandler` | Reuse existing verified legacy profile reconciliation and account-wide deletion coordinator; extend for new journal participants. The profile-only legacy delete handler is not the whole account lifecycle. |
| BFF principal/cache boundary | `AccountController`, `CollectionController`, Gateway `Program` interceptor registration | Integrate the account route's empty owner requests with raw authenticated principal forwarding. Keep legacy collection UUID behavior separate. New diary no-store and nonleaky errors are future owner work, not current pass claims. |
| Platform | `PlatformUserIdentityPropagationInterceptor`, `PlatformGrpcCurrentUser` | Reuse Authorization forwarding. GUID-only currentUser is not canonical account identity; the interceptor forwards a bearer even when GUID UserId is unavailable. Verify installed options/trusted boundaries with host tests; do not duplicate Platform auth. |
| Media | `StorefrontPrivateArtworkOwnerValidator`, `ResourceAccessGrpcInterceptor` registration | Extend the existing fail-closed purpose/tenant owner-check pattern. PlushInquiry authorization does not authorize diary assets. Customer's CheckExperienceMediaOwner producer precedes Media's consumer in EJ-06. |
| Catalog | Revision 2 pinned ProductRelease/specification query sources, fresh main and existing Catalog discovery MR | Integrate current Product/release and exact CatalogId/CategoryId attribute bindings; add only a proven missing projection. Customer owns explicit private unknown mapping. No new attribute engine or cross-service SQL FK. |
| Review and rating | Revision 2 pinned Review handlers/configuration/aggregate calculator, fresh main | Extend independent allowlisted public copies in later gates; preserve existing Review proof/Pending/uniqueness/editing rules and aggregate formulas. Private diary sessions do not vote. |
| Storefront/UI | Existing ProductExperience island/editor and typed draft, BlogPost entity/same-handle fallback, pinned themes and fresh UI main | Extend existing editor/island with a diary mode and shortcut while keeping summary mode. The current same-handle article fallback is not a stable Product relation; EJ-07editorial must coordinate its real producer. |
| Admin/Reference/Architecture | Accepted owner scopes, current refs and source package matrix | Reuse scoped moderation/reference units and canonical architecture ownership. No private staff diary endpoint, no policy invention and no duplicate global catalog. Refresh a bounded owner delta before any later code. |

Detailed observed source declarations and findings: [source/package/policy matrix](source-package-policy-matrix.json). The matrix contains 355 source-declared DKH package versions across 12 repositories. It is not a deployed binary inventory.

## Verification actually run

On the isolated CustomerService source worktree, `.NET SDK 10.0.401` restored and compiled the selected integration test project against current packages. Its assets resolve 40 DKH packages; exact versions and named tests are in [baseline verification](baseline-verification.json).

Command: `dotnet test tests/DKH.CustomerService.IntegrationTests/DKH.CustomerService.IntegrationTests.csproj -c Release --filter 'FullyQualifiedName~CustomerAccountGrpcServiceTests|FullyQualifiedName~GlobalCustomerAccountHandlerTests|FullyQualifiedName~ProductCollectionPrivacyTests'`.

Result: 26 passed, 0 failed, 0 skipped. These are existing account, gRPC host fixture and private collection baselines. Their EF provider is **InMemory**. They do not prove real PostgreSQL constraints, transactions, CAS, cascades, actual JWT/provider forwarding, diary endpoints or production isolation. New diary tests run: zero.

Full existing solution test run: 31 application tests plus 127 integration fixtures passed (158 total, zero failed/skipped); see [full baseline receipt](full-baseline-verification.json). The selected 26 above are a subset, not 26 additional distinct tests. [Revalidated source pins](source-pin-revalidation.json): all 33 accepted file bodies equal fresh main bytes, including files whose repository main advanced.

Pinned OpenSpec CLI `validate --all --strict`: one existing `private-structured-product-experience` change passed. This is baseline validation only. The additive [experience-journal-owner proposal](../../openspec/changes/experience-journal-owner/proposal.md) and its ownership scenarios were authored for EJ-01 and passed strict validation. All implementation tasks remain unchecked; no behavior was implemented. Further dated core/producer deltas belong to their exact subsequent owner gates.

Solution restore/build (Release): passed with zero warnings/errors. Source formatting verification: passed after restoring the complete solution; the first no-restore attempt lacked unit-test project dependencies. No source file changed.

## Disposable PostgreSQL schema preflight

[Schema preflight receipt](postgresql-schema-preflight.json) records a new isolated PostgreSQL 18.4 database with no production data. Existing migrations applied successfully through `dotnet ef database update` to the explicit disposable endpoint; all 11 migration-history entries through `20260921012532_AddPrivateStructuredProductExperience` and actual columns/indexes/foreign keys were read back. The current model has no pending migration changes. Containers were stopped and removed after every attempt.

Two exported SQL-script paths failed: idempotent output reported syntax near `END IF`, and the ordinary concatenated script reported syntax before the migration-history INSERT. The actual EF migration runner succeeded; do not represent the exported scripts as usable deployment paths. The EF CLI 10.0.10 emitted an older-patch warning against runtime 10.0.12; no global tool was changed. This preflight verifies current source migrations on an empty disposable database, not deployed schema, existing-data backfill, rollback or the new journal schema. No generated migration file was edited.

The observed current collection FK points to `customer_profiles.Id`. The profile factory generates that ID separately, while the BFF collection route sends its legacy `RequireUserId` result and handlers use the supplied ID directly. Therefore a verified identity bridge/compatibility journey must be proved in EJ-01/EJ-11; source/schema existence and InMemory tests do not prove that the deployed legacy caller ID equals the profile key. Do not use that legacy UUID route as the authority for new canonical diary ownership.

## Policy, runtime and rollout limits

P02: an approved destination license/version, attribution, withdrawal and consent/audit retention remains unverified; public intake/enablement stays blocked until its owner evidence exists.

P06: `pg-backup.sh` source has configurable defaults (7 daily, 4 weekly, 3 monthly) and preserves failed-backup recovery points. These are not verified deployed retention or provider erasure deadlines. Reuse Customer's account deletion coordinator, but do not claim backup/provider deletion from it. Matching pilot enablement requires actual owner evidence. Independent additive private implementation remains permitted behind disabled flags.

Production-applied PostgreSQL state, deployed package versions, principal forwarding options, Media ACL/direct download, effective persisted themes, actual UI journeys/load and all new diary race/negative/compatibility gates are unverified. Source declarations, fixture tests and synthetic package renders cannot substitute for them.

## Discovery supplement

[Current model/wire and complete Review mutation/report-audit inventory](discovery-delta-2026-10-08.md) resolves the independent source-discovery follow-up; its newer exact Review/Platform refs apply to those named seams. The original receipt above remains a dated observation. Acceptance still requires the supplement review and delivery evidence.

## Handoff and closure

Next: obtain independent review and authoritative CI/main acceptance of the entry receipt, then run EJ-01's exact Customer/BFF owner scopes with validated deltas and WIP=1. Backend dated-core implementation is EJ-02 only after EJ-00/EJ-01 acceptance. Register owner-local implementation tasks before coding. Retain separate policy/runtime enablement gates; do not mark all 24 stages done from this discovery.

EJ-00 remains in progress until its named acceptance and delivery evidence are reconciled. No stage checkbox is checked and no Bead is closed by authoring this receipt.
