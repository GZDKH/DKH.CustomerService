## Why

The accepted Experience Journal adds account-wide dated private entries alongside the existing collection summary. New owner RPCs need the existing canonical issuer/string-subject resolver and an explicit personal-purpose boundary without reinterpreting legacy CustomerProfile IDs or granting private access through staff/service permissions.

## What Changes

- Specify an additive Customer-owned personal journal ownership boundary using the validated authenticated principal and current canonical account/status resolver.
- Require independently resolved owner predicates for future personal resources; browser IDs and wildcard grants confer no ownership.
- Reuse verified legacy account/profile/membership links only for compatibility copy/link operations; unresolved legacy mapping does not reject a fresh entry for an already resolved account.
- Preserve existing verified-email provisioning and legacy collection wire, authorization, summary replacement and rating behavior.
- Authoring this delta changes no executable code, schema, configuration or live policy. Its implementation follows accepted EJ-00 delivery and EJ-01's owner-local registration.

## Capabilities

### New Capabilities

- `experience-journal-owner`: Canonical personal journal owner resolution, status/purpose isolation and verified legacy compatibility boundaries.

### Modified Capabilities

None. Existing collection-summary requirements remain unchanged; this delta applies only to the new journal purpose.

## Impact

Owner: DKH.CustomerService API/Application boundary. StorefrontGateway remains a separate consumer verification/adapter scope in EJ-01. No Platform duplication or producer package/API change is implemented by this specification.

Discovery decision: **extend**, with **integrate** at the BFF/Platform seam. Source `c176ebea92c5500e2cc31646dc1d38ee440408c8` already contains `CustomerAccountGrpcService.ResolveIdentity`, `CustomerAccountHandlerSupport.RequireAccountAsync`, verified profile reconciliation and account-wide deletion. `ProductCollectionGrpcService` uses the existing legacy CustomerId caller check and remains separate. Exact refs, active MRs/worktrees, 33 fresh byte-equal source pins, 40 resolved Customer dependencies, baseline tests and limitations are recorded in [EJ-00 entry discovery](../../../docs/product-experience-journal/entry-gate-2026-10-08.md).

Accepted C1 authority: [Revision 2 contracts](https://gitlab.xnata.com/gzdkh/agents/DKH.AgentRules/-/blob/f01aad0eae8cc0e3517f70b08c2b7abf699c98f4/plans/product-experience-journal/package/readiness/CONTRACTS.en.md). Tracking remains in Beads/GitLab, not this behavioral spec. Phase issue: CustomerService #8; isolated branch `docs/8-journal-entry-gate-01a117e3` is this session's no-code entry/specification worktree.

Non-goals: dated schema/core commands, new protobuf/REST endpoints, editor/UI, media upload/publication/withdrawal, public author aliases, account provisioning policy changes, email merges, production reads/writes/configuration, or pilot enablement. P02/P06 remain separate later enablement evidence gates.
