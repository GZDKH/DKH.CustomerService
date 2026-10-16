## Context

Reuse accepted Customer52 independent-part handoff67697c75 and current main781e/fbc19. Existing CustomerJournalOwnerResolver.RequireIdentity verifies captured named-Bearer personal evidence; CustomerJournalAccounts.ResolveAsync already enforces unique primary/verified alias ownership. Neither file is modified.

## Decisions

Add CurrentProfileBindingService in v2 and an empty GetMyActiveProfileBinding request. Return a direct model containing CustomerProfileId and StorefrontId only. Do not add an AuthorityRef or timestamp revision. API uses named Bearer plus existing self-access policy; store selector is exact-one nonzero and must agree with Platform context.

Reuse account resolver, then perform one fresh nontracked projection which rechecks identity candidate uniqueness/current alias, active nondeleted account, unique active nondeleted membership in the selected storefront, exact LegacyCustomerProfileId, same-account/same-storefront Linked and active profile. All final predicates are in one read command; no long-lived eligibility promise or mutation. No transaction, Ensure, reconcile, SaveChanges, contact callback or new schema.

## Risks and verification

EF InMemory CPU checks behavior but not PostgreSQL translation. Add isolated cases to the existing ordinary CI PostgreSQL fixture, without running local resources. Test exact distinct IDs, missing/ambiguous/foreign/revoked/deleted state and fresh second reads. Auth tests cover actual validated token boundary and selector rejection. Verify old proto bytes and normal additive version publication. Existing old173/main45038 observation remains its owner; new merge/SDK/runtime follow prerequisite qualification and normal gates.
