## 1. Contract and specification

- [x] 1.1 Add the private structured experience protobuf model and additive request/response fields.
- [x] 1.2 Bump DKH.CustomerService.Contracts minor version and validate the OpenSpec change.

## 2. Domain and persistence

- [x] 2.1 Add normalized experience, tag, and typed observation entities with domain validation.
- [x] 2.2 Add EF configurations, relationships, indexes, and migration without changing existing collection rows.
- [x] 2.3 Add application commands/handlers/mappers for save, replace, preserve, and clear semantics.

## 3. Verification and exchange

- [x] 3.1 Add domain/application tests for one-of values, zero/false, clear, invalid input, and owner isolation.
- [x] 3.2 Add gRPC contract/integration coverage and update customer exchange DTO/schema for private owner export/import.
- [x] 3.3 Run format, build, test, and pending-migration gates; merge the issue-linked MR and verify the authoritative post-main pipeline.
