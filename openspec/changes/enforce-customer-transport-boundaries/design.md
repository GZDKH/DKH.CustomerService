## Context

See proposal.md for the verified983e84f source and scoped discovery. The current four verification methods share existing repository/provider ports and Domain verified-state operations. Customer already uses typed MediatR requests returning existing wire responses; Program registers the Application assembly through AddPlatformMessagingWithMediatR. The existing Application registration method provides the scoped port; no provider infrastructure is added.

## Goals / Non-Goals

Move the whole existing verification flow across the layer boundary, then enforce all actual owner transports with individual reviewed roles. Preserve the four methods' wire behavior and all existing authentication metadata. Provider delivery/activation and tenant/subject policy changes remain separate50/51; delivery-contact52/56 and journal ownership remain separate.

## Decisions

Use one cohesive IContactVerificationUseCases Application port with four typed methods, implemented by ContactVerificationUseCases using the two existing repository/provider ports. Register it through the existing Application scoped-registration method. The self-access policy/caller-matching attributes and storefront resolution stay at ingress.

Published DKH.Platform.Messaging.MediatR1.2.1 XML documents the globally registered relational outbox transaction behavior; its implementation wraps the handler in the database execution strategy. Routing external verification through new MediatR requests would change the old provider/transaction boundary and could repeat the external call during a database retry. The direct Application port preserves the old flow and normal repository save behavior. No read-only marker, transaction middleware change or alternate provider is introduced. Other Customer CQRS paths retain their existing Platform pipeline.

Keep contact comparison in one cohesive Application helper: whitespace-empty values mismatch; comparison trims both values, ignores email case, uses ordinal phone matching, and sends the actual stored profile contact and profile.Id.ToString() to the provider. Preserve NotFound, mismatch error text/code, nullable-result normalization, expiry and caller cancellation. Only successful confirmation mutates verified state and invokes the existing repository update with the same token. No new validator or policy tightens old request behavior as a side effect.

The transport returns each typed use-case response. Keep existing transport regressions connected to the real Application implementation; add use-case success/failure, identity and cancellation cases. The existing AddApplication method registers the scoped port while infrastructure retains its existing repository/provider registrations.

Whole-owner enforcement requires actual registered CLR/compiler inventory, explicit per-operation Application or technical roles, exact typed request/result/caller-token binding and compiled positive/refusal controls from the genuine canonical engine. Candidate inventory/source inspection is not semantic proof. Resolve Customer's exact canonical input identity and accepted publisher distribution before full qualification; never edit a receiving engine copy or invent acceptance.

## Risks / Trade-offs

- Contact/provider behavior drift: retain existing negative/provider-unavailable cases and test stored-contact/profile-id forwarding and success-only persistence.
- Authentication changes while moving use cases: retain all original ingress policies/attributes and current storefront selection; do not claim separate51 is fixed.
- Unaccepted compiler input: keep owner qualification pending and use only genuine accepted distribution for final enforcement.
- Concurrent service features: touch only verification flow/tests and owner-local enforcement, preserving52/56 and other owners.

## Migration Plan

No contract/package, event, schema, configuration or consumer migration. Run required affected projects/format/Release/real isolated provider checks at the end, then current-source review, common MR/source CI, authorized merge, authoritative main/compiler-role-runner evidence and owned cleanup. Roll back through the prior compatible service source if necessary; no data deletion or provider switch. Production business acceptance remains final.

The source983e84f baseline recipient56 evidence manifest used the unsupported stage authoring. Normalize only that field to the equivalent unqualified draft stage so the existing repository-wide consistency gate can run. Preserve its task states, evidence, owner and acceptance; no notification implementation or completion is taken over.

Add the missing exact-commit evidence CI include at delivered AgentRules801a8ee (authoritative push-main success verified). Its bundled checker needs no private-source download or extra credential. All8 opted-in changes now pass the unchanged consistency contract; the separate56 stage normalization changes no task state or acceptance. This is a CI enforcement change only, with no runtime configuration change.
