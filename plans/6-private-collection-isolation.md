# Private collection isolation verification

## Decision

Extend the existing ProductCollection gRPC contract and owner binding with an integration-only verification slice. No new persistence, contract, cache, or authorization behavior is introduced.

## Plan

1. Exercise the real gRPC ProductCollection service with two authenticated principals sharing one in-memory database.
2. Persist a structured private experience as customer A and prove customer A can read it.
3. Attempt list/read/update/delete using customer B and customer A identifiers; require `PermissionDenied` before handler access.
4. Re-read as customer A to prove the private note remains unchanged.
5. Run formatter, focused integration tests, build, and full service gates before MR.
