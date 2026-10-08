## Context

See proposal.md and the EJ-00 source receipt. Canonical account resolution already exists in CustomerService. The owner boundary is currently embedded in the account gRPC service and account handler support, while collection consumers keep a legacy UUID caller contract. Platform forwards the bearer independently of its GUID current-user convenience view. The additive journal must preserve those existing boundaries.

## Goals / Non-Goals

Goals: share the existing trusted principal/account/status path within the new Customer journal purpose, reuse verified legacy links for compatibility, and prevent generic grants or untrusted browser identifiers from supplying ownership.

Non-goals: implement dated journal tables/commands/APIs, change existing account provisioning or collection wire semantics, add an account/auth service, duplicate Platform token validation, or enable production/pilot flags. This entry MR authors specifications and evidence only.

## Decisions

1. Reuse the existing configured issuer plus validated raw authenticated subject resolution. Extract or compose the existing owner resolver in the API/Application boundary when EJ-01 implementation starts; do not parse the subject into a CustomerProfile UUID or reuse a browser AccountId. Reusing the existing resolver preserves current issuer and account-status behavior; a new account service or GUID fallback would create a conflicting authority.
2. Resolve an active canonical account independently at the owner service for each permitted journal operation. Private owner predicates remain mandatory even when staff/resource permission grants are broad. The personal-versus-service purpose check must use authoritative validated identity/context evidence, never caller-controlled headers, email guessing or a generic wildcard permission. Reuse an established Platform mechanism where one exists; verify its actual registered/resolved version and host behavior before accepting the implementation.
3. Keep compatibility links purpose-limited. Reuse current verified account/profile/membership reconciliation. Missing or quarantined legacy links block only the requested copy/link, while a resolved canonical account can create a fresh private entry once the later core/API gates exist. Do not invent an alias table until a bounded owner/consumer gap proves it necessary.
4. Preserve the legacy summary surface and existing verified-email provisioning. New journal authorization applies to its new purpose; it must not rewrite old collection or existing account APIs. Producer protobuf/package work remains EJ-03 and the BFF adapter is a separate owner scope.

## Risks / Trade-offs

- An InMemory or overridden-principal fixture can hide relational or deployed JWT errors → retain current baseline evidence as limited proof; add purpose/negative host tests and required real PostgreSQL checks in the relevant implementation gates.
- A GUID-only convenience current-user API drops a non-GUID subject → use the existing raw authenticated issuer/subject resolver and test the bearer forwarding path explicitly.
- Service/staff permission alone can be mistaken for private ownership → fail closed on missing authoritative personal-purpose evidence; owner predicates are independent of generic grants. This is an acceptance requirement, not a claim that a deployed classification mechanism was verified in this source audit.
- Legacy reconciliation is incomplete or ambiguous → preserve old route behavior and deny only compatibility import/link until verified.

## Migration Plan

This specification/entry receipt changes no application or database state and needs no migration. Later EJ-01 refactoring is additive and retains current account/collection contracts; package/API changes follow the producer-before-consumer sequence when their exact gates start. Rollback of subsequent journal intake must retain owner reads, export/delete/withdrawal and deny/cleanup participants. No user data is dropped to roll back a UI.
