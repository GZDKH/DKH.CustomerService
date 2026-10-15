## Context

Customer issue52 is an additive owning-service companion to original plan row83 (V6.7) / DeliveryService#2, not a new original140 task. Existing Customer.Contracts1.10 GetAddress returns mutable current fields. It supplies neither immutable authority nor delivery consent. Counterparty Notification leases are not shipping permissions. Existing authenticated personal ownership and account/profile reconciliation are reused; journal business paths and foreign journal work remain unchanged.

## Goals / Non-Goals

Persist explicit contact observations, recipient consent/revocation and short access verification receipts. Bind actual current authenticated ownership, preserve exact retries and provide least-data contracts. Do not invent group recipients, grants, historical revision1, timestamps-as-versions, global account/profile/JWT equivalence, phone verification, future availability, legal rights, production grants, or a second identity/authorization framework.

## Decisions

- New additive delivery_contact.v2 uses permanent tags, real GuidValue choice_revision_id, one Delivery duty AuthorityRef, strict command operation/expected_revision/reason, and a normal additive1.11.0 source version transition. Existing v1 descriptors are unchanged; package publication belongs only to normal main CI.
- The existing named Bearer validated personal token capture supplies issuer/sub. Current primary or verified alias resolves exactly one active CustomerAccount. Active storefront membership must bind an active reconciled CustomerProfile; address.CustomerId is that profile, not account or JWT subject.
- Explicit Seal creates a real current observation and persisted numeric revision; it does not backfill purported history. Only references and canonical hashes are retained. Current name/address/phone must still match on every access and retry; changed/deleted source or newer observation makes older authority unavailable.
- Three new owner tables hold observations, consent and short access receipts. They do not cascade to deletable source profiles/addresses. Native guards forbid history mutation/deletion, except exact consent revocation. Populated Down refuses55000 before any DDL; empty Down reverses only this migration.
- Existing Platform MediatR transactional/outbox behavior owns local transactions. Store requires its relational transaction, serializes owner changes, and locks/rechecks source rows. The producer does not start a new host, transport, database or external background worker.
- Consent is recipient self-issued intent for exact group/allocation/plan/scope/choice/duty and expires within five minutes. It is not a Delivery grant. Leases expire within five minutes and earlier real consent/Delivery expiry. An owner-scoped revocation-operation lookup and database unique index fence cross-consent changed-target/body reuse. Same operation and same body replay the original result only after fresh current checks; changed body, expired/revoked source or foreign actor cannot renew it.
- Self liability Resolve returns opaque immutable reference plus a distinct short verification observation. That observation expiry is not authority validity or duty expiry. Underlying availability remains conditional on fresh current state; no future horizon is promised.
- Recipient access needs a genuine Delivery owner callback under the SAME authenticated human bearer. Delivery must echo the exact typed scope, verified recipient CustomerProfile binding, storefront/workspace, carrier phone need and expiry. Customer independently resolves profile-to-account and current address/consent. Genuine Delivery.Contracts1.14.0 is now published from main5c5f76d7275c001e4c9a60582c7926f1a6e9691b (package30461). Customer integrates its generated GroupContactAuthorityService client only when the explicit endpoint is configured. A current verified personal token is required and the same human bearer is forwarded by the existing Platform interceptor with UseServiceIdentity=false and retries disabled. Every callback must match the full typed scope, storefront and actual nonempty profile/workspace/consent/contact references with a live bounded expiry. Customer additionally compares the real consent.Id and immutable contact.ResourceId, then independently rechecks its own current contact source and consent. Missing endpoint retains UnavailableDeliveryContactScopeAuthorizer; missing/unimplemented/denied/stale owner authorization fails AUTHORITY_UNAVAILABLE. The actual Delivery callback/participant producer is still an upstream runtime acceptance requirement; no synthetic positive adapter is registered.
- Least-data recipient output contains name, postal address, optional carrier-required phone, access ID, expiry and scope revision. It excludes email, billing, account/customer/order IDs. Owner profile binding is explicit private metadata from Seal; it is never labelled an accepted Delivery participant identity without an actual producer.

## Risks / Trade-offs

No distributed atomic revocation is claimed: Delivery and Customer current checks are separate owner operations. Source hashes preserve exact observed bytes/fields, not historic contact content. The short lease cannot establish future availability. Shipping integration stays OPEN until genuine owner callback, participant identity source and normal SDK ordering are delivered; no circular fake package or raw-wire client is introduced. PostgreSQL cases are compiled/discovered locally and must execute in ordinary CI; no local native proof is claimed.

## Migration Plan

Apply the additive three-table migration through normal service delivery. There is no bulk legacy backfill and no production data mutation in this task. Preserve all pre-existing tables/history. Populated downgrade fails closed; an explicit separately accepted history-retention plan would be required to remove adopted authority history.

## Open Questions

Delivery's genuine group/allocation participant producer and authenticated callback publication remain upstream acceptance dependencies. They are concrete missing owner bindings, not permission to trust request bodies.

## Delivery callback consumer continuation

The issue52 continuation uses a fresh main-based branch after the externally merged MR172, without changing existing contracts or persisted tables. The only new runtime key is `Platform__Grpc__Endpoints__GroupContactAuthorityServiceClient__Url`; Infrastructure owns its normal deployment projection. No default localhost or invented route activates the adapter. Configuration availability is not evidence that a genuine participant authority exists. Native tests use an explicit synthetic owner port and remain ordinary-CI acceptance, not production-positive proof.
