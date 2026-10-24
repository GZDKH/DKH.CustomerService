## ADDED Requirements

### Requirement: Ordinary addresses belong to the actual active selected profile
The service SHALL bind ordinary Get/List/Create/Update/Delete/GetDefault/SetDefault to the actual nondeleted CustomerProfile with the selected StorefrontId and UserId and active AccountStatus. Address reads and mutations SHALL require Address.CustomerId to equal that profile. Ordinary Get/Update/Delete/Default SHALL additionally require a nondeleted address. Explicit query predicates SHALL enforce this independently of automatic soft-delete filters and tracked FindAsync values.

#### Scenario: Foreign or unavailable address
- **WHEN** a real address belongs to another user or storefront, or its owner profile is missing/deleted/inactive, or the ordinary address is deleted
- **THEN** no address data is disclosed or mutated and the existing unavailable/not-found response is preserved

#### Scenario: Legitimate owner
- **WHEN** the selected profile and address are active and have the exact stored ownership link
- **THEN** the existing ordinary operation succeeds without changing other owners or deleted historical addresses

### Requirement: Explicit inactive address views and admin lifecycle preserve their boundaries
An explicit inactive/all list SHALL remain limited to the same active owning profile. Restore and permanent-delete SHALL retain their existing admin-only policy and protocol; no new self restore or broader authority SHALL be introduced.

#### Scenario: Owned inactive list
- **WHEN** an eligible owner explicitly requests inactive or all addresses
- **THEN** only that profile's requested address state is returned, with no foreign/deleted-profile disclosure

#### Scenario: Existing administration
- **WHEN** the existing authorized admin restores or permanently deletes an address
- **THEN** the existing lifecycle protocol and authorization requirements remain intact

### Requirement: Current caller and storefront are preserved through asynchronous disclosure
The ordinary address service SHALL retain its existing self-claim interceptor and role policy. A conflicting body storefront SHALL NOT override an already resolved current storefront. After awaited dispatch and before disclosure, the original authenticated caller/context/claims, selected storefront and cancellation SHALL remain current. A refusal after a committed mutation SHALL NOT claim rollback.

#### Scenario: Late request-context change
- **WHEN** caller identity/claims/context/storefront changes or cancellation occurs while an address operation awaits
- **THEN** the pending result is refused without returning private address data or issuing another operation
