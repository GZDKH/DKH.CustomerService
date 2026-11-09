## Context

Reuse CustomerJournalAccounts and CurrentActiveProfileBinding with explicit
issuer/opaque subject/storefront/profile selectors. These inputs are DATA, not
caller permission; only the actual signed Notification service principal may
access the new method. Preserve every existing HUMAN/admin/DeliveryContact gate.

## Decision

A single read-only source resolves the current unique active owner/profile,
projects only native contact/preferences and canonical primary identity,
rechecks binding/source around awaits and enforces a short caller-bound read
window. No provisioning, reconciliation, database writes or verification occur.
Canonical CustomerAccount.VerifiedEmail/EmailVerifiedAt supplies email; a
missing profile override uses that already-established primary contact, while
a different explicit profile email is unavailable until genuine owner
verification exists. Legacy ContactVerification flags cannot approve it.
Profile channel preferences remain authoritative. Returning canonical identity
lets Notification locate its native identity-scoped preferences/devices without
casting CustomerProfileId to a user UUID; actual supported issuer/subject mapping
belongs to that consumer. No Telegram ID is invented from profile.UserId.

The new v2 RPC captures machine identity only in the existing successful
JwtBearer OnTokenValidated chain, checks service:v1/actual Notification client
and account identity, trusted configured issuer and original caller expiry,
and rechecks request-local caller state around source waits. Shape validation
of an already validated token is not another JWT signature validator. Existing
Platform authentication and service identity own signatures/tokens/config.
New data/DTOs confer no buyer, payment, delivery, consent or broker authority.

Current native source reads and caller window must still be valid on return.
ObservedAt/ReadValidUntil describe a short data read, never permission to send
later. Notification must resolve/revalidate current source/preferences at actual
dispatch, persist its original event/business-key handling and record real
provider outcomes. Consumer-first rollout is required. Source publication is
not full integration, delivery or V6.2 acceptance.

## Verification

Actual PostgreSQL active/alias/profile/storefront/current preference/revocation
and no-write controls; validated JWT direct boundary positives/negative callers
and request-local expiry/mutation; descriptor/wire/API compatibility, scoped
format/build/OpenSpec/security. Reuse the existing isolated native fixture and
actual bearer validation test seam. Never run real provider sends in authoring.
