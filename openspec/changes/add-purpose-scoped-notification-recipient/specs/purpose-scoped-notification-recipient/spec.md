## ADDED Requirements

### Requirement: Current recipient data requires the exact active owner binding
The source SHALL resolve current primary/verified-alias account ownership and
one exact active storefront/profile binding before disclosing contact or
preferences, and SHALL reject revoked/foreign/mismatched or ambiguous sources.
It SHALL NOT provision, reconcile or modify a customer.

#### Scenario: Alias, profile and preference changes are current native facts
- **WHEN** Notification resolves an admitted participant selector
- **THEN** current native ownership and exact profile/storefront are checked
- **AND** revoked or changed sources are refused rather than served from tracked state.

### Requirement: A notification service identity does not impersonate a buyer
Only the actual authenticated Notification machine SHALL read the private
method. Human, forged, other-client and changed/expired request callers SHALL
be denied. Existing human self/admin and DeliveryContact methods SHALL retain
their current admission.

#### Scenario: Private selectors are not authority
- **WHEN** another caller supplies a valid participant issuer/subject/profile
- **THEN** no contact is disclosed and no caller is substituted for that buyer.

### Requirement: Native contact preference is distinct from provider delivery
The source SHALL use the canonical account verified email and exact profile
channel preferences, never legacy verification flags or an invented Telegram
recipient. A different explicit profile email SHALL remain unavailable rather
than silently choosing another address. The result SHALL retain primary
identity namespace and a bounded observed read window, never equate user/profile
IDs or establish consent, payment, provider success or delivery.

#### Scenario: Disabled or unverified-override email is a legitimate no-send outcome
- **WHEN** the profile disables email or selects a different email lacking a
  genuine native verification source
- **THEN** the model contains no destination email and the explicit eligibility
  outcome explains the current native state
- **AND** actual Notification event preferences/dispatch and provider results remain required.
