## Why

Original V6.2/Commerce#88 requires private participant-specific budget notices.
Commerce29ab5dc retains admitted recipient selectors; Notification cannot use
Customer HUMAN self preferences or admin profile searches as service authority.
Supporting Customer#56 owns the necessary least-privilege recipient DATA source.

## What Changes

- Extend the existing active account/profile binding reader with current
  purpose-scoped recipient/contact/preferences DATA for Notification only.
- Add a private v2 service with an authenticated Notification machine boundary,
  no human impersonation or widening of existing endpoints.
- Reuse canonical verified CustomerAccount email and its native verification
  time, governed by exact profile channel preferences. A different storefront
  email does not become verified through legacy profile flags or silently fall
  back to another address; return explicit unverified-override eligibility.
- Preserve canonical account identity as DATA for existing Notification account
  preference/device lookup; never equate profile/account/Keycloak identifiers.
- Add required additive Contracts1.12→1.13 transition; no schema mutation,
  verification operation, provider send or manual SDK publication.

## Existing Implementation

Decision: extend/integrate. Customer main f80c0d6f has
CustomerJournalAccounts/CurrentActiveProfileBinding for current primary and
verified-alias ownership and exact active profile/membership; CustomerAccount
owns verified email from the verified identity flow. Profile.ContactVerification
has the separately tracked NullVerificationService concern#50 and is not a
substitute for canonical email verification. CurrentProfileBinding RPC is HUMAN
SELF; CustomerPreferences methods enforce caller UserId. DeliveryContact is a
separate purpose, not a notification contact grant. Notification main d0ea6690
has native event preferences/devices and delivery engines but no inspected
Customer recipient contract. Its generic request takes ready contacts/OrderId.
Literal/semantic discovery, current contracts, open issues/MRs and leases were
checked before this decision; no complete private notification equivalent was
identified. Customer#56 has its own freshly leased branch, not a journal takeover.

## Capabilities

### New Capabilities
- `purpose-scoped-notification-recipient`: current private recipient DATA bound
  to the original account identity, storefront and exact admitted profile.

### Modified Capabilities

## Impact

Customer Application/API/Contracts and current native/JWT boundary tests;
EN/RU integration docs. Actual Notification receiving, private queue/route,
current preference/device checks, provider outcomes and consumer-first rollout
follow in that owner's coordinated source. Original V6.2 remains open.
