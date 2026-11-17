## Why

A7.1 requires thin transport boundaries. On Customer main983e84f7379cf6b646410fea4c867583418d5b6c, the four ContactVerificationGrpcService operations directly read ICustomerRepository, invoke the verification port and mutate/persist verified state. Those existing use cases belong in Application.

## What Changes

- Integrate the current complete email/phone initiation and confirmation flows into a typed Application use-case port, preserving existing observable behavior.
- Keep authenticated self-access, caller matching and existing storefront resolution at ingress; reuse the existing repository/provider ports, Domain operations and Application scoped registration.
- Complete the actual owner transport inventory and individual compiler-bound application/technical dispositions using the genuine accepted engine. Preserve the explicit prerequisite if Customer compiler admission/distribution is not yet accepted.
- Add real compiled refusal controls and preserve/extend behavioral regressions before full delivery.

Discovery decision: **integrate** existing use cases, then **extend** owner-local boundary enforcement. No duplicate verification provider or business policy is introduced.

## Capabilities

### New Capabilities

- `customer-transport-boundaries`: fail closed when owned transports perform business/persistence orchestration, with explicit typed dispatch and individually justified technical roles.

### Modified Capabilities

None. Existing contact-verification contract requirements are preserved.

## Impact

Only Customer Application, the existing verification transport, affected tests and local enforcement/evidence. No public protobuf, package/version, runtime configuration, schema, event or gateway change. Existing contracts remain customer/api/contact_verification/v1/contact_verification_service.proto and customer/models/contact_verification/v1/contact_verification.proto; legacy compatibility files remain untouched.

Evidence: ContactVerificationGrpcService.cs blobb3c6f385f2fe81cc68b4e840a3844a2dbc4864b8 at the main revision above; Application/Abstractions/ICustomerRepository.cs and IVerificationService.cs; Domain/ValueObjects/ContactVerification.cs; Program.cs AddPlatformMessagingWithMediatR(typeof(ConfigureServices).Assembly); existing ContactVerificationGrpcServiceTests and UnavailableVerificationServiceTests. Deterministic ContactVerification/IVerificationService discovery and direct body/contract review found the current API flow, not an existing equivalent Application handler. The transport's package/namespace and wire methods are unchanged for both gateways.

Trackers: [Customer57](https://gitlab.xnata.com/gzdkh/services/DKH.CustomerService/-/issues/57), [original147](https://gitlab.xnata.com/gzdkh/agents/DKH.AgentRules/-/issues/147). Branch feat/57-transport-boundaries-01a1b561 in the same-named leased repository worktree. Fresh all-state A7.1 search found no existing Customer issue; no open MR or local Customer lease existed before this claim.

Non-goals: provider activation/replacement (separate50), tenant/subject policy changes (separate51), delivery-contact authority/recipient work (52/56), private journals, and production business acceptance. These owners and contracts remain separate. No candidate compilation or local test is claimed as accepted publisher/maintained/main delivery evidence.
