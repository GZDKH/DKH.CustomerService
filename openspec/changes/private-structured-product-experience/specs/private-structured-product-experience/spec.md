## Purpose

Provides a generic, owner-private structured product experience that can be filled from any catalog definition set, including tea, without making the platform depend on a product theme.

## ADDED Requirements

### Requirement: Owner can save a typed private experience

The service SHALL allow the collection owner to attach one optional private experience to an existing collection item, including an optional experience timestamp, personal text, tags, recommendation text, and typed observation rows keyed by stable definition IDs.

#### Scenario: Save a complete experience

- **WHEN** the authenticated owner adds or updates a collection item with an experience containing preparation observations, flavor observations, tags, text, timestamp, rating, and recommendation
- **THEN** the service persists and returns the same typed values and existing 1–5 rating

#### Scenario: Preserve explicit scalar presence

- **WHEN** an observation contains decimal zero or boolean false in its declared scalar slot
- **THEN** the returned model preserves that value as present rather than treating it as unset

#### Scenario: Reject malformed observation

- **WHEN** an observation has an empty definition ID, more than one scalar slot, no scalar slot, an invalid role, or an overlong value
- **THEN** the service rejects the request with InvalidArgument and does not partially persist the experience

### Requirement: Owner can update and clear the private experience

The service SHALL distinguish an omitted experience message from a present empty experience message.

#### Scenario: Omitted experience preserves draft

- **WHEN** an owner updates only status, notes, or rating and omits the experience message
- **THEN** the existing private experience remains unchanged

#### Scenario: Present empty experience clears draft

- **WHEN** an owner sends an explicitly present empty experience message
- **THEN** the experience and its child observations and tags are removed while the collection item and rating remain

### Requirement: Private experience is owner-isolated

The service SHALL return structured experience data only for the owning customer and SHALL keep it out of public product and review projections.

#### Scenario: Different customer cannot read or update

- **WHEN** a different customer requests or updates the collection item
- **THEN** the service returns PermissionDenied and reveals no experience values

#### Scenario: Public review remains separate

- **WHEN** a product review is listed publicly
- **THEN** the private experience is not included unless a later explicit share operation creates a ReviewService review

### Requirement: Additive compatibility is preserved

The service SHALL keep existing collection clients and the current 1–5 rating behavior working when no experience is supplied.

#### Scenario: Legacy collection request

- **WHEN** an existing client adds or updates a collection item using only status, notes, and rating
- **THEN** the request succeeds with no experience and the rating retains the existing 1–5 validation
