# DKH.CustomerService

Customer profile, addresses, and wishlist management microservice for Telegram Mini App storefronts. Handles profiles, delivery addresses, wishlists, preferences, contact verification, and GDPR compliance.

- **Framework**: .NET 10.0
- **Architecture**: Clean Architecture + CQRS (MediatR)
- **Transport**: gRPC (6 services, ~30 methods)
- **Database**: PostgreSQL (EF Core 10)

## Documentation

- [Architecture Docs (EN)](https://github.com/GZDKH/DKH.Architecture/blob/main/en/services/backend/customer-service-index.md)
- [Architecture Docs (RU)](https://github.com/GZDKH/DKH.Architecture/blob/main/ru/services/backend/customer-service-index.md)
- [Local Docs](./docs/README.md)

## Quick Start

```bash
# Restore and build
dotnet restore
dotnet build -c Release

# Run tests
dotnet test

# Start PostgreSQL
docker compose up -d dkh.customerservice-db

# Apply migrations
dotnet ef database update \
  --project DKH.CustomerService.Infrastructure \
  --startup-project DKH.CustomerService.Api

# Run the service
dotnet run --project DKH.CustomerService.Api
```
