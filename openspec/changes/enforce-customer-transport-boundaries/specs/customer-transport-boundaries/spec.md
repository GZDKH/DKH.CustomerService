## Purpose

Keep Customer transport operations limited to authenticated request/response adaptation while preserving the observable contact-verification contract and enforcing actual current owner coverage.

## ADDED Requirements

### Requirement: Owned transport enforcement is complete and fails closed

The owner boundary check SHALL inventory every actual registered transport operation and require an individual reviewed Application or technical disposition. Business/persistence orchestration, undeclared operations and invalid request/result/caller bindings MUST fail. Unknown or unaccepted compiler inputs MUST NOT qualify delivery.

#### Scenario: A transport performs persistence beside a valid dispatch
- **WHEN** a compiled current-source transport reads or writes storage while also calling an allowed use case
- **THEN** enforcement fails with a source-bound finding

#### Scenario: A request or caller binding is substituted
- **WHEN** a current-source mutation replaces the declared request, caller cancellation, authenticated actor or consumed result
- **THEN** the relevant compiled refusal control fails the altered binding

#### Scenario: A new transport is added without disposition
- **WHEN** the actual registration inventory gains an undeclared operation
- **THEN** the owner check refuses qualification

### Requirement: Verification relocation preserves the current contract

The relocation SHALL preserve authenticated self-access/caller matching, existing storefront resolution, profile lookup, contact comparison, provider invocation, replies and cancellation. Confirmation MUST persist verified state only after a successful provider response.

#### Scenario: Profile is missing
- **WHEN** the resolved storefront and user have no current profile
- **THEN** the operation returns the existing NotFound status and does not invoke the provider

#### Scenario: Requested contact differs from the profile
- **WHEN** trimmed email differs ignoring case or trimmed phone differs ordinally
- **THEN** the existing contact-mismatch reply is returned without provider invocation or persistence

#### Scenario: Contact matches
- **WHEN** the current contact matches and initiation or confirmation proceeds
- **THEN** the provider receives the stored profile contact, that profile's identifier and the original caller cancellation token, with the existing code forwarded for confirmation

#### Scenario: Confirmation succeeds
- **WHEN** the provider confirms the contact successfully
- **THEN** the matching verified flag is set and the profile is updated with the original caller token

#### Scenario: Provider fails or is unavailable
- **WHEN** initiation or confirmation returns a failure
- **THEN** the existing error/expiry reply is preserved and confirmation does not mark or persist verified state
