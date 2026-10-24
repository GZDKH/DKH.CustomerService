# Enforce address ownership and active state

## Why

Fresh Customer main `38502250120371371671d373c35556408f3bfff0` uses address-only `FindAsync` in ordinary Get/Update/Delete. The active List path loads all address navigation rows while the actual Address model has no active query filter and Platform automatic soft-delete filters default to false. The authenticated request UserId check therefore does not establish ownership of the selected AddressId.

## Discovery decision

Decision: **fix** the existing ordinary address-handler family. Existing Get/List/Create/Update/Delete/Default contracts already carry the selected StorefrontId/UserId; the normal gRPC service keeps `RequireCallerMatchesClaim`. Reuse the actual profile/address FK and active account status. Scoped deterministic discovery: `/private/tmp/customer-address-discovery-01a166de.json`; fresh Customer main and existing Delivery consumer reviewed. No matching active MR; broad Customer#51 remains open, foreign Customer#52 and journal leases are preserved. GitLab owner: gzdkh/services/DKH.CustomerService#53, prerequisite of gzdkh/agents/DKH.AgentRules#136.

## Changes

Use explicit current active-profile/address ownership predicates for the ordinary address family, retain explicit owned inactive list modes and existing admin-only restore/purge, and retain the original caller/context across asynchronous service work. Reject a body storefront conflicting with an already resolved current storefront. No RPC/SDK/schema/permission/profile/key or global query-filter changes.

## Acceptance

Foreign user/storefront, missing/deleted/inactive profile and deleted ordinary address do not disclose or mutate data; legitimate own operations and supported inactive lists/admin lifecycle remain. Tests exercise existing handler/public boundaries with synthetic persisted EF/native data and current-caller changes. Independent current-source review, installed full normal checks, CI, merge, authoritative main/deploy/runtime and owned cleanup remain required. This bounded bug fix does not close the broader Customer#51 audit or any original commerce140 item.
