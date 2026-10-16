## Purpose

Allow a verified personal caller to read its actual current active CustomerProfile binding in one selected storefront without provisioning or granting downstream authority.

## ADDED Requirements

### Requirement: Verified self-only current profile
The service SHALL return only the actual stored active linked CustomerProfile identifier and selected storefront for the authenticated personal named-Bearer issuer and subject. The request MUST NOT accept account/profile/user identifiers as authority. Account, membership and profile identifiers MUST remain distinct.

#### Scenario: Existing linked active owner
- **WHEN** a validated personal token resolves uniquely to an active nondeleted account with one active nondeleted membership and an exact linked active profile in the selected storefront
- **THEN** the service returns that CustomerProfile.Id and storefront without creating or changing any rows

#### Scenario: Missing or ambiguous owner state
- **WHEN** primary/verified alias ownership is ambiguous, or account, membership or linked profile is missing, blocked, revoked, deleted, foreign or not linked
- **THEN** the service refuses without disclosing other owners or repairing the data

#### Scenario: Fresh revocation
- **WHEN** a previously eligible binding is revoked or unlinked before a subsequent read
- **THEN** that subsequent read refuses using current untracked database state

### Requirement: Trusted identity and exact storefront scope
The service SHALL reuse the existing validated personal named-Bearer identity boundary and SHALL require one nonempty storefront selector consistent with the resolved platform context. It MUST reject forged claims, service principals and duplicate, malformed or conflicting selectors.

#### Scenario: Invalid caller or selector
- **WHEN** validated personal evidence is absent or a storefront selector is duplicated, malformed, empty or conflicts with resolved context
- **THEN** the service refuses before any profile is exposed

### Requirement: Observation privacy and compatibility
The additive v2 read SHALL expose no contact or personal data, revision, digest, validity, grant or recipient/participant permission. Existing RPC routes, tags and behavior SHALL remain compatible. Consumers MUST freshly revalidate the observation and separately establish their own authority.

#### Scenario: Minimal observation
- **WHEN** a self binding is returned
- **THEN** only actual profile and storefront identifiers are present, with no inferred account/profile alias or future authority
