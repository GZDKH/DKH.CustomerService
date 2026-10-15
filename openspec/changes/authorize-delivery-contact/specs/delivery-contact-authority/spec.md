## Purpose

Provide private current-owner contact authority and recipient consent without exposing mutable address reads as immutable delivery permission.

## ADDED Requirements

### Requirement: Explicit current owner observation
The system SHALL seal only an authenticated active owner's currently linked storefront profile and owned address. It SHALL persist a real numeric revision and exact digest, never derive a version from timestamps or invent legacy history.

#### Scenario: Current owned address
- **WHEN** the verified personal actor seals their linked active address
- **THEN** a persisted immutable reference and explicit CustomerProfile binding are returned

#### Scenario: Foreign or changed source
- **WHEN** owner, storefront, linked profile, address, active state or exact retained source no longer matches
- **THEN** sealing, resolution and replay SHALL fail without foreign contact disclosure

### Requirement: Recipient consent and revocation
The system SHALL bind self-issued consent to the exact contact, group, allocation, plan UUID, scope revision, choice UUID, duty acceptance reference and local-shipment purpose. Consent SHALL expire within five minutes and support versioned explicit revocation.

#### Scenario: Revoked or different scope
- **WHEN** consent is revoked, expired or presented for a different scope/version/contact
- **THEN** it SHALL authorize no recipient disclosure or renewed lease

### Requirement: Current independent Delivery authority
The system SHALL require fresh genuine Delivery-owner authorization under the same human bearer before recipient contact access or revalidation. It SHALL compare the echoed exact scope and actual profile binding. Caller selectors SHALL NOT become authority.

#### Scenario: Missing producer boundary
- **WHEN** genuine callback or recipient identity binding is unavailable
- **THEN** access SHALL fail AUTHORITY_UNAVAILABLE without returning contact data

### Requirement: Least-data short verification
Recipient output SHALL contain only name, postal address, carrier-required phone, opaque access ID, expiry and scope revision. Sessions SHALL expire within five minutes or earlier genuine expiries. Self liability verification SHALL expose no address and SHALL NOT redefine duty expiry or promise future contact availability.

#### Scenario: Retry and expiry
- **WHEN** the same operation is retried or an existing session is revalidated
- **THEN** fresh current owner/source/consent/Delivery checks SHALL precede replay and SHALL NOT extend the original expiry

### Requirement: Atomic history and compatible delivery
The system SHALL retain authority metadata and deduplication results atomically, reject changed-body replay, preserve existing v1 APIs and refuse populated downgrade before destructive DDL.

#### Scenario: Rollback or populated downgrade
- **WHEN** the local transaction rolls back or populated downgrade is attempted
- **THEN** no partial authority receipt SHALL persist and adopted history SHALL remain intact

#### Scenario: Concurrent history adoption and downgrade
- **WHEN** a writer commits contact history while downgrade waits for its retained-table locks
- **THEN** downgrade SHALL observe that history after the writer and refuse before removing guards or tables

#### Scenario: Truncate and changed-target replay
- **WHEN** retained tables are truncated or a revocation operation is reused for another consent or changed request
- **THEN** history SHALL remain intact and the second consent SHALL remain unchanged
