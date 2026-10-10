## Why

Accepted Customer C1/C2 provides canonical ownership and an append-only private journal, but public typed owner RPCs, protected cursors and their failure boundaries are absent. EJ03 must expose unknown/General operations without duplicating those producers or granting Product writes before EJ04.

## What Changes

- Add a typed journal owner transport capability over accepted C1/C2, with lossless decimal/date/presence/time-offset mapping and bounded owner-safe profile/history responses.
- Specify trusted-origin admission, authenticated cursors, source-bound Copy retries, stable committed-response projection and fail-closed Product operations.
- Preserve ProductCollection, account provisioning and all prior schema/lifecycle semantics.
- Deliver only discovery evidence and an OpenSpec proposal/design/spec/tasks in this MR. Runtime and release work require separate coordinator admission.

## Capabilities

### New Capabilities

- `typed-journal-owner-transport`: additive owner-authenticated typed unknown/General RPCs and their cursor, replay and Product adapter boundaries. Existing C1/C2 code is reused; the new capability names the missing public boundary, not a second journal implementation.

### Modified Capabilities

None. Existing living specs, earlier changes and historical task4.2 are unchanged. New delta is confined to this change.

## Impact

Decision: **extend**. [Evidence packet](../../../docs/discovery/ej03-typed-owner-contracts/01a1245f/evidence.json) pins Customer324b97e2, AgentRules95725ff6, 17 source references and accepted C1–C3/RT03. [Decision](../../../docs/discovery/ej03-typed-owner-contracts/01a1245f/decision.md) describes discovered producers, contract/client checks and explicit gaps. Owner issue: https://gitlab.xnata.com/gzdkh/services/DKH.CustomerService/-/issues/10; original stage: https://gitlab.xnata.com/gzdkh/agents/DKH.Beads/-/issues/12177.

Owned branch `docs/12177-ej03-readonly-discovery-01a1245f`, checkout `.worktrees/docs-12177-ej03-readonly-discovery-01a1245f`, executor01a1245f, lease until2026-10-11T06:12:29Z. Coordinator retains Beads/graph/lease authority. No repeated discovery or old-owner access.

Future implementation affects only Customer owner contracts/API and bounded producer extensions. Later BFF consumes the published package under a separate stage; Platform key/context configuration and release producer gates require their own admission. Current package1.10.0 remains unchanged;1.11.0 is a candidate only. Non-goals: second REST backend, caller-owned AccountId, Catalog writes/adapters, new journal tables/receipt engine, BFF/UI/Media/publication, legacy endpoint replacement, production calls, new credentials or release/deploy/merge in this docs MR.
