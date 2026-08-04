# DKH.CustomerService -- Domain Model

## Overview

DKH.CustomerService owns a global, Keycloak-backed customer account plus lazy
storefront memberships. Existing storefront-scoped profiles continue to own
addresses, wishlists, preferences, statistics, and merchant-specific state
during the additive migration. The principal aggregates are
`CustomerAccountEntity`, `StorefrontMembershipEntity`, and the compatible
`CustomerProfileEntity`.

## Class Diagram

```mermaid
classDiagram
    class CustomerAccountEntity {
        +Guid Id
        +string IdentityIssuer
        +string IdentitySubject
        +string VerifiedEmail
        +DateTime EmailVerifiedAt
        +string? FirstName
        +string? LastName
        +string PreferredLocale
        +CustomerAccountStatusType Status
        +ICollection~LinkedCustomerIdentityEntity~ LinkedIdentities
        +Create()
        +UpdateVerifiedEmail()
        +UpdateProfile()
        +LinkIdentity()
        +AnonymizeForDeletion()
    }

    class LinkedCustomerIdentityEntity {
        +Guid Id
        +Guid CustomerAccountId
        +string ProviderAuthority
        +string ProviderSubject
        +string ProviderKind
        +string? DisplayName
        +DateTime LinkedAt
        +DateTime VerifiedAt
    }

    class StorefrontMembershipEntity {
        +Guid Id
        +Guid CustomerAccountId
        +Guid StorefrontId
        +Guid? LegacyCustomerProfileId
        +DateTime FirstAuthenticatedAt
        +DateTime LastAuthenticatedAt
        +DateTime LastActivityAt
        +StorefrontMembershipStatusType Status
        +Create()
        +RegisterAuthenticatedTouch()
        +RevokeAndDelete()
    }

    class CustomerProfileEntity {
        +Guid Id
        +Guid StorefrontId
        +string UserId
        +string ProviderType
        +string FirstName
        +string? LastName
        +string? Username
        +string? PhotoUrl
        +string? Phone
        +string? Email
        +string LanguageCode
        +bool IsPremium
        +bool AllowsWriteToPm
        +Guid? CustomerAccountId
        +CustomerAccountReconciliationStatusType AccountReconciliationStatus
        +AccountStatus AccountStatus
        +ContactVerification ContactVerification
        +CustomerPreferences Preferences
        +ICollection~CustomerAddressEntity~ Addresses
        +ICollection~WishlistItemEntity~ WishlistItems
        +ICollection~CustomerExternalIdentityEntity~ ExternalIdentities
        +Create()
        +Update()
        +UpdateFromTelegram()
        +AddExternalIdentity()
        +RemoveExternalIdentity()
        +SetPrimaryIdentity()
        +MergeFrom()
        +SoftDelete()
        +Anonymize()
    }

    class CustomerAddressEntity {
        +Guid Id
        +Guid CustomerId
        +string Label
        +string Country
        +string City
        +string? Street
        +string? Building
        +string? Apartment
        +string? PostalCode
        +string? Phone
        +bool IsDefault
        +Create()
        +Update()
        +SetDefault()
    }

    class WishlistItemEntity {
        +Guid Id
        +Guid CustomerId
        +Guid ProductId
        +Guid? ProductSkuId
        +DateTime AddedAt
        +string? Note
        +Create()
        +UpdateNote()
    }

    class CustomerExternalIdentityEntity {
        +Guid Id
        +Guid CustomerId
        +string Provider
        +string ProviderUserId
        +string? Email
        +string? DisplayName
        +bool IsPrimary
        +DateTime LinkedAt
        +Create()
        +SetPrimary()
        +UpdateEmail()
        +UpdateDisplayName()
    }

    class AccountStatus {
        +AccountStatusType Status
        +DateTime? BlockedAt
        +string? BlockReason
        +string? BlockedBy
        +DateTime? SuspendedUntil
        +DateTime? LastLoginAt
        +DateTime? LastActivityAt
        +int TotalOrdersCount
        +decimal TotalSpent
    }

    class ContactVerification {
        +bool EmailVerified
        +DateTime? EmailVerifiedAt
        +bool PhoneVerified
        +DateTime? PhoneVerifiedAt
    }

    class CustomerPreferences {
        +bool EmailNotificationsEnabled
        +bool TelegramNotificationsEnabled
        +bool SmsNotificationsEnabled
        +bool OrderStatusUpdates
        +bool PromotionalOffers
        +string PreferredLanguage
        +string PreferredCurrency
    }

    class AccountStatusType {
        <<enumeration>>
        None = 0
        Active = 1
        Blocked = 2
        Suspended = 3
        Deleted = 4
    }

    CustomerAccountEntity "1" --> "*" LinkedCustomerIdentityEntity : Linked identities
    CustomerAccountEntity "1" --> "*" StorefrontMembershipEntity : Memberships
    StorefrontMembershipEntity "0..1" --> "0..1" CustomerProfileEntity : Legacy profile
    CustomerAccountEntity "0..1" --> "*" CustomerProfileEntity : Reconciled profiles
    CustomerProfileEntity "1" --> "*" CustomerAddressEntity : Addresses
    CustomerProfileEntity "1" --> "*" WishlistItemEntity : WishlistItems
    CustomerProfileEntity "1" --> "*" CustomerExternalIdentityEntity : ExternalIdentities
    CustomerProfileEntity --> AccountStatus
    CustomerProfileEntity --> ContactVerification
    CustomerProfileEntity --> CustomerPreferences
    AccountStatus --> AccountStatusType
```

## Entities

### CustomerAccountEntity

The global aggregate keyed uniquely by the trusted Keycloak issuer and
subject. It is not storefront-scoped. Verified email is mutable profile data,
not an account merge key.

| Property | Type | Constraints | Description |
|----------|------|-------------|-------------|
| `IdentityIssuer` | `string` | Required, max 512 | Server-configured Keycloak issuer |
| `IdentitySubject` | `string` | Required, max 256 | Validated JWT `sub` within the issuer |
| `VerifiedEmail` | `string` | Required, max 256 | Email accepted only with `email_verified=true` |
| `EmailVerifiedAt` | `DateTime` | Required | When verified proof was accepted |
| `FirstName` / `LastName` | `string?` | Max 100 | Shared core profile names |
| `PreferredLocale` | `string` | Required, max 16 | Shared locale |
| `Status` | `CustomerAccountStatusType` | Required | Active, blocked, or deletion pending |

The unique key is `(IdentityIssuer, IdentitySubject)`. Full-account deletion
anonymizes the authoritative identity, email, linked identities, memberships,
and all reconciled storefront profile data before soft deletion.

### LinkedCustomerIdentityEntity

A globally unique reference to a provider identity linked after verified
provider proof. The unique key is `(ProviderAuthority, ProviderSubject)`.
Self-service responses intentionally omit both raw values and expose only safe
provider kind, display name, and timestamps.

### StorefrontMembershipEntity

The lazy association created on the first authenticated visit to a storefront.
It implements `IPlatformStorefrontScoped`, is unique by
`(CustomerAccountId, StorefrontId)`, and may point to one reconciled legacy
profile. Deleting one membership anonymizes only that storefront's data;
deleting the global account anonymizes every membership.

### CustomerProfileEntity

The aggregate root representing a customer profile scoped to a storefront. Implements `FullAuditedEntityWithKey<Guid>`, `IAggregateRoot`, and `IPlatformStorefrontScoped`.

| Property | Type | Constraints | Description |
|----------|------|-------------|-------------|
| `Id` | `Guid` | PK | Unique identifier |
| `StorefrontId` | `Guid` | Required | Multi-tenancy scope |
| `UserId` | `string` | Max 64 | External user identifier |
| `ProviderType` | `string` | Max 50, default `"Telegram"` | Authentication provider type |
| `FirstName` | `string` | Max 100 | Customer first name |
| `LastName` | `string?` | Max 100 | Customer last name |
| `Username` | `string?` | Max 100 | Username / handle |
| `PhotoUrl` | `string?` | Max 512 | Profile photo URL |
| `Phone` | `string?` | Max 32 | Phone number |
| `Email` | `string?` | Max 256 | Email address |
| `LanguageCode` | `string` | Max 10 | Preferred language code |
| `IsPremium` | `bool` | Default `false` | Premium account flag |
| `AllowsWriteToPm` | `bool` | Default `false` | Telegram permission flag for proactive PM writes |
| `CustomerAccountId` | `Guid?` | Nullable FK | Proven global account link |
| `AccountReconciliationStatus` | `CustomerAccountReconciliationStatusType` | Required | Pending, processing, linked, or quarantined |
| `AccountReconciliationAttemptCount` | `int` | Default `0` | Restartable reconciliation attempt count |
| `LastAccountReconciliationAttemptAt` | `DateTime?` | Optional | Last migration attempt |
| `AccountReconciliationReasonCode` | `string?` | Max 64 | Privacy-safe quarantine reason code |
| `AccountStatus` | `AccountStatus` | Value object | Account status and activity tracking |
| `ContactVerification` | `ContactVerification` | Value object | Email/phone verification state |
| `Preferences` | `CustomerPreferences` | Value object | Notification and display preferences |
| `Addresses` | `ICollection<CustomerAddressEntity>` | Navigation | Delivery addresses |
| `WishlistItems` | `ICollection<WishlistItemEntity>` | Navigation | Wishlist items |
| `ExternalIdentities` | `ICollection<CustomerExternalIdentityEntity>` | Navigation | Linked external identities |

**Methods:**

| Method | Description |
|--------|-------------|
| `Create` | Factory method for creating a new customer profile |
| `Update` | Updates profile fields (name, phone, email, etc.) |
| `UpdateFromTelegram` | Syncs profile data and Telegram capability flags from Telegram user info |
| `AddExternalIdentity` | Links a new external identity provider |
| `RemoveExternalIdentity` | Unlinks an external identity |
| `SetPrimaryIdentity` | Marks an external identity as primary |
| `MergeFrom` | Merges data from another customer profile (account linking) |
| `SoftDelete` | Marks the profile as deleted without physical removal |
| `Anonymize` | Removes direct PII and anonymizes child addresses, wishlist notes, and external identities for GDPR compliance |

### CustomerAddressEntity

A delivery address belonging to a customer. Implements `FullAuditedEntityWithKey<Guid>`.

| Property | Type | Constraints | Description |
|----------|------|-------------|-------------|
| `Id` | `Guid` | PK | Unique identifier |
| `CustomerId` | `Guid` | FK -> customer_profiles | Owning customer |
| `Label` | `string` | Max 64 | Address label (e.g., "Home", "Work") |
| `Country` | `string` | Max 100 | Country name |
| `City` | `string` | Max 100 | City name |
| `Street` | `string?` | Max 256 | Street address |
| `Building` | `string?` | Max 32 | Building number |
| `Apartment` | `string?` | Max 32 | Apartment / unit number |
| `PostalCode` | `string?` | Max 20 | Postal / ZIP code |
| `Phone` | `string?` | Max 32 | Contact phone for delivery |
| `IsDefault` | `bool` | | Whether this is the default address |

**Methods:**

| Method | Description |
|--------|-------------|
| `Create` | Factory method for creating a new address |
| `Update` | Updates address fields |
| `SetDefault` | Marks this address as the default |

### WishlistItemEntity

A product added to a customer's wishlist. Implements `FullAuditedEntityWithKey<Guid>`.

| Property | Type | Constraints | Description |
|----------|------|-------------|-------------|
| `Id` | `Guid` | PK | Unique identifier |
| `CustomerId` | `Guid` | FK -> customer_profiles | Owning customer |
| `ProductId` | `Guid` | Required | Product reference |
| `ProductSkuId` | `Guid?` | Optional | Specific SKU variant |
| `AddedAt` | `DateTime` | Required | When the item was added |
| `Note` | `string?` | Max 512 | Customer note about the item |

**Methods:**

| Method | Description |
|--------|-------------|
| `Create` | Factory method for adding a wishlist item |
| `UpdateNote` | Updates the note on a wishlist item |

### CustomerExternalIdentityEntity

An external identity provider linked to a customer (e.g., Google, Apple, email). Implements `FullAuditedEntityWithKey<Guid>`.

| Property | Type | Constraints | Description |
|----------|------|-------------|-------------|
| `Id` | `Guid` | PK | Unique identifier |
| `CustomerId` | `Guid` | FK -> customer_profiles | Owning customer |
| `Provider` | `string` | Max 50 | Identity provider name |
| `ProviderUserId` | `string` | Max 256 | User ID at the provider |
| `Email` | `string?` | Max 256 | Email from the provider |
| `DisplayName` | `string?` | Max 200 | Display name from the provider |
| `IsPrimary` | `bool` | | Whether this is the primary identity |
| `LinkedAt` | `DateTime` | Required | When the identity was linked |

**Methods:**

| Method | Description |
|--------|-------------|
| `Create` | Factory method for creating a new external identity link |
| `SetPrimary` | Marks this identity as the primary one |
| `UpdateEmail` | Updates the email from the provider |
| `UpdateDisplayName` | Updates the display name from the provider |

## Value Objects

### AccountStatus

Tracks the customer's account state, activity metrics, and administrative actions.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Status` | `AccountStatusType` | `None` | Current account status |
| `BlockedAt` | `DateTime?` | `null` | When the account was blocked |
| `BlockReason` | `string?` | `null` | Reason for blocking |
| `BlockedBy` | `string?` | `null` | Admin who blocked the account |
| `SuspendedUntil` | `DateTime?` | `null` | Suspension expiry date |
| `LastLoginAt` | `DateTime?` | `null` | Last login timestamp |
| `LastActivityAt` | `DateTime?` | `null` | Last activity timestamp |
| `TotalOrdersCount` | `int` | `0` | Lifetime order count |
| `TotalSpent` | `decimal(18,2)` | `0` | Lifetime spending total |

### ContactVerification

Tracks verification state for email and phone contacts.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EmailVerified` | `bool` | `false` | Whether email is verified |
| `EmailVerifiedAt` | `DateTime?` | `null` | When email was verified |
| `PhoneVerified` | `bool` | `false` | Whether phone is verified |
| `PhoneVerifiedAt` | `DateTime?` | `null` | When phone was verified |

### CustomerPreferences

Notification channels and display preferences for the customer.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EmailNotificationsEnabled` | `bool` | `true` | Receive email notifications |
| `TelegramNotificationsEnabled` | `bool` | `true` | Receive Telegram notifications |
| `SmsNotificationsEnabled` | `bool` | `false` | Receive SMS notifications |
| `OrderStatusUpdates` | `bool` | `true` | Receive order status updates |
| `PromotionalOffers` | `bool` | `false` | Receive promotional offers |
| `PreferredLanguage` | `string` | `"en"` | Preferred UI language |
| `PreferredCurrency` | `string` | `"USD"` | Preferred display currency |

## Enums

### AccountStatusType

| Value | Name | Description |
|-------|------|-------------|
| 0 | `None` | Default / unset |
| 1 | `Active` | Active customer |
| 2 | `Blocked` | Blocked by admin |
| 3 | `Suspended` | Temporarily suspended |
| 4 | `Deleted` | Soft-deleted |

## Domain Events

Domain events are published via MediatR through the `DomainEvents` collection on the aggregate root. Events are dispatched after successful persistence to ensure consistency.

*Last updated: August 2026*
