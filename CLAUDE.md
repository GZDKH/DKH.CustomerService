# CLAUDE.md

## Required Reading (MUST read before working)

Before starting any task in this repository, you MUST read these files from DKH.Architecture:

1. **[AGENTS.md](https://github.com/GZDKH/DKH.Architecture/blob/main/AGENTS.md)** — baseline rules for all repos
2. **[agents-dotnet.md](https://github.com/GZDKH/DKH.Architecture/blob/main/docs/agents-dotnet.md)** — .NET specific rules
3. **[github-workflow.md](https://github.com/GZDKH/DKH.Architecture/blob/main/docs/github-workflow.md)** — GitHub Issues & Project Board

These files are located in the DKH.Architecture repository (located in the sibling `libraries/DKH.Architecture` folder relative to your workspace).

---

<!-- BEGIN LOCAL-CLAUDE-RULES -->

## Additional Local Rules (.claude/rules)

Before starting implementation, you MUST also read and follow these local rule files in this repository:

- `.claude/rules/build-before-commit.md`
- `.claude/rules/commits.md`
- `.claude/rules/github-tasks.md`
- `.claude/rules/gitlab-workflow.md`
- `.claude/rules/no-duplication.md`
- `.claude/rules/security.md`

These rules are mandatory and complement the baseline `AGENTS.md` and `DKH.Architecture` guidance.

<!-- END LOCAL-CLAUDE-RULES -->



This file provides guidance to Claude Code when working in this repository.

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
