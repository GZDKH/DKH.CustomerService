## Why

DATA13 API76 needs a genuine private contact authority for the accepting actor. DATA10 API70 needs recipient-consented, purpose-scoped local-shipment contact access. Existing Customer GetAddress returns mutable current data and timestamps, not either authority.

## What Changes

- Extend Customer address ownership with explicitly sealed immutable contact revisions; do not relabel historical addresses or timestamps as revisions.
- Add self-owned liability-contact resolution and recipient consent/revocation, with short-lived Resolve/Revalidate leases and current owner/version checks.
- Cross-recipient disclosure requires a genuine current Delivery plan/duty/allocation/choice authorizer. Its missing transport binding fails closed; internal verified records and caller assertions do not replace it.
- Preserve existing address APIs, account rights, journal work, and public responses. Do not expose email, billing details or full Customer identity.

## Capabilities

### New Capabilities
- `delivery-contact-authority`: immutable Customer-owned contact revisions and bounded purpose/consent access.

### Modified Capabilities
None.

## Impact

Owning Customer#52 is a companion to original plan row83 (V6.7) / DeliveryService#2, not an additional original140 task. Decision: **extend** the existing Customer address/account ownership seams. Fresh base is `42c6abb97bccda9bf4e4bfc77a1e6c0e8110e5b8`, branch `feat/52-delivery-contact-authority-01a13ae1` in its matching leased worktree. Existing implementation evidence is the current CustomerAccountIdentityResolver, CustomerAccountHandlerSupport, CustomerAddressEntity, GetAddress handler and Contracts1.10 package29893; bounded discovery is `/tmp/customer-delivery-contact-companion-01a13ae1/customer-existing-discovery.json`. Existing GetAddress has no immutable revision, purpose or recipient consent.

Delivery owns actual plan/duty/choice authorization and independently checks liability ownership. Sibling contract ACK preserves API76 AuthorityRef tag9 and API70 LOCAL_SHIPMENT input/output semantics. Counterparty Notification invitations are explicitly excluded. No journal/EJ03 extension, new external credentials, grants activation, production access or synthetic positive adapter is included. Full V6.7 acceptance remains with the integrated owners.
