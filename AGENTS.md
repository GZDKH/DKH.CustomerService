# AGENTS.md

<!-- BEGIN REQUIRED-READING -->

## Required Reading (MUST read before working)

Before starting any task in this repository, read the shared DKH.AgentRules entrypoint:

1. **[AGENTS.md](../../agents/DKH.AgentRules/AGENTS.md)** — shared Codex entrypoint and on-demand trigger index

Profiles, skills, build gates, contracts, releases, and docs rules are lazy-loaded from `agents/DKH.AgentRules`. Use `../../agents/DKH.AgentRules/rules/codex/triggers.md` to decide what else to open for the current task.

---

<!-- END REQUIRED-READING -->
This file provides guidance to Codex when working in this repository.

> **Baseline rules**: See `AGENTS.md` for unified GZDKH rules (SOLID, DDD, commits, code style, quality guardrails). This file adds service-specific context only.

## Project Overview

DKH.CustomerService is a .NET 10 microservice for managing Telegram Mini App customer data: profiles, delivery addresses, wishlists, preferences, and account status with multi-tenancy support.

- **Framework**: .NET 10.0
- **Architecture**: Clean Architecture + CQRS (MediatR)

## Build Commands

```bash
# Build and test
dotnet restore
dotnet build -c Release
dotnet test

# Format check
dotnet format --verify-no-changes

# Run locally
dotnet run --project DKH.CustomerService.Api

# Apply migrations
dotnet ef database update \
  --project DKH.CustomerService.Infrastructure \
  --startup-project DKH.CustomerService.Api

# Docker (start DB first)
docker compose up -d dkh.customerservice-db
```

## Architecture

**Project Structure:**
- `DKH.CustomerService.Domain` — Entities, value objects, enums
- `DKH.CustomerService.Application` — MediatR handlers, validators, mappers
- `DKH.CustomerService.Infrastructure` — EF Core DbContext, repositories
- `DKH.CustomerService.Api` — gRPC services, Program.cs, DI setup
- `DKH.CustomerService.Contracts` — Protobuf schemas in `proto/customer/`

**Key Patterns:**
- CQRS via MediatR (Commands, Queries, Handlers)
- FluentValidation for all commands/queries
- Value objects for AccountStatus, ContactVerification, CustomerPreferences
- Multi-tenancy via StorefrontId
- GDPR compliance (soft-delete, anonymization, data export)

## gRPC Services

Proto files in `DKH.CustomerService.Contracts/proto/customer/{api|models}/v1/`:

**Services (6 total, ~30 methods):**
- `customer_profile_service.proto` — GetOrCreate, Get, Update, Delete profiles
- `customer_address_service.proto` — CRUD for delivery addresses
- `wishlist_service.proto` — Add, Remove, Get, List, Clear wishlist items
- `customer_preferences_service.proto` — Notification settings, language, currency
- `customer_admin_service.proto` — Admin: search, block, unblock, GDPR export/delete
- `contact_verification_service.proto` — Email/phone OTP verification

**Models:**
- `customer_profile.proto`, `customer_address.proto`, `wishlist_item.proto`
- `customer_preferences.proto`, `account_status.proto`, `contact_verification.proto`

After changes: `dotnet build DKH.CustomerService.Contracts`

## Configuration

- `ConnectionStrings:Default` — PostgreSQL (dkh_customers database)
- Port: API `5010`

## External Dependencies

- PostgreSQL via EF Core 10
- DKH.Platform shared libraries
- gRPC consumers: StorefrontGateway, AdminGateway

## Related Repositories

- `DKH.Platform` — Shared libraries
- `DKH.Architecture` — Architecture documentation
- `DKH.StorefrontGateway` — Storefront consumer (Profile, Addresses, Wishlist APIs)
- `DKH.AdminGateway` — Admin API consumer (Customer management)
- `DKH.OrderService` — Uses addresses for delivery
- `DKH.CartService` — Links via CustomerId
