## Why

Commerce group ownership needs the authenticated human's actual active CustomerProfile.Id. Existing GetCustomerAccount and membership contracts return distinct account/membership identifiers and cannot authorize a profile alias.

## What Changes

- Extend existing verified personal identity/account resolution with a private read-only current profile projection in the selected storefront.
- Add one additive v2 GetMyActiveProfileBinding RPC returning only actual profile and storefront identifiers; reuse named Bearer validation and fail closed on ambiguous, revoked, deleted or unlinked state.
- Preserve all existing account, journal, contact and callback behavior; do not create or reconcile any identity/profile.

## Capabilities

### New Capabilities
- `current-active-profile-binding`: self-only current owner observation without invented revisions or contact/participant grants.

### Modified Capabilities
None.

## Impact

CustomerService only; Commerce and Delivery can adopt only after genuine SDK publication and their own current authority checks. No schema/data/provider/consent changes. Contract addition is minor; publisher-owned allocation must be verified before release.

## Existing implementation and decision

Decision: **extend**. Fresh main781e1aa52d9502ce228d94ce49fd0c00a69209e7 contains accepted fbc19. Deterministic discover-existing with active profile binding / LegacyCustomerProfileId and current issue/MR/lease readback reused handoff67697c7500989d3fcdcca8a880282aa966384a1559028c26d46ccf6635a7c340. CustomerJournalPrincipal.Require/CustomerJournalOwnerResolver.RequireIdentity already validate personal named Bearer iss/sub; CustomerJournalAccounts.ResolveAsync selects unique primary/verified alias. DeliveryContactStore.Ownership.RequireProfileAsync establishes actual linked membership/profile predicates but is contact-transaction-specific and remains unchanged. Existing v1 CustomerAccountService omits profile linkage. Customer52 owner retains only old173/main45038 observation; this fresh disjoint leased branch owns the new producer. Full original83 remains open.
