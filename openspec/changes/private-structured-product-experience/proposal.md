## Why

The existing private product collection can only keep one free-text note and a 1–5 rating. That cannot represent a detailed personal product experience or be rendered from the same catalog definitions used by the storefront, while placing structured values in `Notes` would make them unvalidated and impossible to export safely.

## What Changes

- Extend the existing ProductCollectionService v1 model with one optional owner-private structured product experience.
- Store experience metadata, personal text, tags, recommendation text, and typed observation rows keyed by stable catalog definition IDs.
- Keep each typed row's scalar value and optional unit explicit; preserve null, zero, false, omitted, and clear semantics.
- Return the experience from existing collection add/update/get operations while preserving current clients and the existing 1–5 rating.
- Add persistence migration and domain/application validation for duplicate definitions, one-of scalar values, lengths, and ownership.
- Include the private experience in the existing customer data exchange contract only after it is represented as first-class collection data; it must never enter public review/product projections.

## Capabilities

### New Capabilities

- `private-structured-product-experience`: owner-private structured product observations and recommendation attached to an existing collection item.

### Modified Capabilities

- None.

## Impact

- DKH.CustomerService.Contracts protobuf model and version metadata.
- CustomerService domain, application handlers/mappers, EF configuration and migration.
- Customer data exchange DTO/schema/export/import coverage for collection experience.
- Existing ProductCollectionService consumers remain source-compatible because all new fields are additive.
- ReviewService and storefront public projections are unchanged; explicit publication is a later integration through the existing review flow.

Discovery decision: **extend**. Existing implementation evidence at fresh origin/main: `ProductCollectionItemEntity` only has `Notes` and `Rating`; ProductCollectionService v1 already owns add/update/get; ReviewService owns public reviews; no private structured experience exists.
