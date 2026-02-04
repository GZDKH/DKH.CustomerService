# DKH.CustomerService Documentation

## Overview

DKH.CustomerService manages Telegram Mini App customer data with multi-tenancy support.

## Domains

| Domain | Description |
|--------|-------------|
| CustomerProfile | Extended user profile on top of Telegram data |
| CustomerAddress | Delivery addresses with default address support |
| WishlistItem | Customer's product wishlist |
| CustomerPreferences | Notification settings and preferences |
| AccountStatus | Account status, blocking, activity tracking |
| ContactVerification | Email and phone verification status |

## gRPC Services

| Service | Methods | Description |
|---------|:-------:|-------------|
| CustomerProfileService | 4 | Profile CRUD with soft-delete |
| CustomerAddressService | 7 | Address management with default support |
| WishlistService | 6 | Wishlist management |
| CustomerPreferencesService | 4 | Notification preferences |
| CustomerAdminService | 8 | Admin operations, GDPR compliance |
| ContactVerificationService | 4 | Email/phone OTP verification |

## Architecture

```
┌─────────────────────┐
│ StorefrontGateway   │
│  - Profile API      │
│  - Addresses API    │
│  - Wishlist API     │
└─────────┬───────────┘
          │ gRPC
          ▼
┌─────────────────────┐     ┌─────────────────────┐
│  CustomerService    │────►│ ProductCatalogSvc   │
│  - Profiles         │     │ (validate ProductId)│
│  - Addresses        │     └─────────────────────┘
│  - Wishlists        │
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│     PostgreSQL      │
│   dkh_customers     │
└─────────────────────┘
```

## Related Documentation

- [Architecture Docs](https://github.com/GZDKH/DKH.Architecture/blob/main/en/services/backend/customer-service-index.md)
- [StorefrontGateway](https://github.com/GZDKH/DKH.StorefrontGateway)
- [OrderService](https://github.com/GZDKH/DKH.OrderService)
