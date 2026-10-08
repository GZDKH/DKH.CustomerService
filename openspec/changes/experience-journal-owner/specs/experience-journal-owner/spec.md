## Purpose

Defines the personal ownership boundary for the additive account-wide journal while retaining existing account provisioning, verified legacy links and collection-summary behavior.

## ADDED Requirements

### Requirement: Canonical account comes from the validated personal principal

New personal journal operations SHALL resolve the canonical account from the trusted configured issuer and authenticated subject through the existing account resolver. A browser-supplied account/customer identifier, email match, GUID parsing assumption or unsigned identity header MUST NOT grant ownership. Existing account status guards SHALL apply.

#### Scenario: Non-GUID subject resolves an existing account

- **WHEN** a validated personal principal has a non-GUID subject and an existing active canonical account
- **THEN** the journal owner boundary resolves that account without interpreting the subject as a CustomerProfile UUID

#### Scenario: Supplied account identifier cannot select a different owner

- **WHEN** a caller supplies a different account/customer identifier or spoofed identity header
- **THEN** that input grants no journal ownership and the caller cannot operate on that account's private resources

#### Scenario: Missing trusted identity fails closed

- **WHEN** the principal is unauthenticated, the authenticated subject is missing, or the trusted issuer is unavailable
- **THEN** ownership is not granted and the operation returns its documented authentication or authoritative-context failure

#### Scenario: Deleted or blocked account cannot gain journal access

- **WHEN** the resolved account is deleted, deletion-pending or blocked
- **THEN** the existing account guard prevents new personal journal access

### Requirement: Personal purpose and owner predicates defeat permission bypass

Every new personal journal resource operation SHALL independently bind to the resolved canonical account and the current permitted purpose. A service credential alone or wildcard staff/resource grant MUST NOT substitute for personal ownership. The boundary SHALL fail closed when required authoritative identity or purpose evidence is unavailable. Foreign and absent personal resources SHALL have the same NotFound response.

#### Scenario: Staff grant does not disclose another owner's data

- **WHEN** a caller has an administrative wildcard grant but targets a different account's private journal resource
- **THEN** the operation returns NotFound and reveals no private data

#### Scenario: Service credential is not personal ownership

- **WHEN** a caller presents only a service credential without the required validated personal ownership context
- **THEN** no personal journal access is granted

#### Scenario: Validated purpose has an exact versioned string shape

- **WHEN** the installed JWT validator accepts a bearer but its purpose is missing, service/unsupported, wrong-version, duplicate, array, object, null or another non-string value
- **THEN** journal access is denied while existing legacy API authorization retains its own policy

#### Scenario: Verified aliases resolve one current account without email guessing

- **WHEN** a current configured issuer and raw subject match a primary identity or an already verified active issuer/subject link
- **THEN** exactly one distinct active account is resolved, and missing, future/unverified, removed or conflicting authority matches fail closed

#### Scenario: Configured external issuer preserves the canonical account namespace

- **WHEN** the installed validator accepts a personal bearer from the exact configured external URL for the same trusted realm
- **THEN** journal ownership resolves into the existing internal configured account namespace, while additional issuer allowlist entries grant no journal authority

#### Scenario: Missing and foreign resources remain indistinguishable

- **WHEN** an authenticated personal caller targets an absent resource or one owned by a different account
- **THEN** both attempts return the same NotFound result without disclosing the other owner

### Requirement: Legacy mapping is verified and limited to compatibility purposes

A compatibility copy, import or later Review link SHALL use only verified existing account/profile/membership identity links. Missing, ambiguous, quarantined or deleted mappings MUST NOT be invented from email or a parsed subject. Lack of a legacy mapping MUST NOT prevent a fresh canonical private entry for an already resolved account.

#### Scenario: Verified account/profile link is reused

- **WHEN** a compatibility operation has an unambiguous verified active profile/membership link for the resolved account
- **THEN** it uses that link within its matching storefront and purpose without creating a second account identity

#### Scenario: Unresolved legacy mapping blocks only its compatibility operation

- **WHEN** legacy mapping is absent, ambiguous or quarantined
- **THEN** the copy/link operation is denied while the already resolved account remains eligible for new canonical private entry operations

### Requirement: Existing provisioning and collection semantics remain compatible

The new journal purpose SHALL preserve the existing verified-email account provisioning guard, existing allowed self-service account reads and legacy collection client authorization and wire semantics. Existing collection experience SHALL remain one replaceable optional summary with nullable experienced date, a 7000-character personal-text limit and separate collection rating; it SHALL NOT become an implicit historical journal writer.

#### Scenario: Provisioning still needs verified email proof

- **WHEN** a new account provisioning request lacks the existing verified-email proof
- **THEN** the existing provisioning guard rejects it without creating or merging an account by email

#### Scenario: Existing self-service read does not add a provisioning requirement

- **WHEN** a valid existing account principal lacks a verified-email claim for an allowed existing self-service read
- **THEN** that read retains its existing account-service behavior

#### Scenario: Legacy collection client remains unchanged

- **WHEN** an old collection client submits an existing summary, omits it, or explicitly clears it
- **THEN** the existing replacement, preserve/clear, nullable date, 7000-character validation and separate rating semantics remain unchanged and no historical journal session is fabricated

#### Scenario: Soft-deleted summaries do not occupy the active summary slot

- **WHEN** an owner replaces or clears the existing optional collection summary and later supplies a new summary
- **THEN** real PostgreSQL accepts at most one active summary for that collection item while retaining soft-deleted rows and separate collection rating
