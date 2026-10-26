## Why

Customer#54 corrects the existing same-human recipient contact authority, rather than adding a grant. At Customer main2497f8f1a08a12a6d20047110d0832435cde2e2d, `DeliveryContactStore.ResolveRecipientAsync` holds the resolved account lock while calling Delivery. Delivery's current callback re-enters Customer via its physical execution authority, waiting on the same account. `ResolveActorAsync` also discards the actual locked CustomerProfile.Id required to compare the accepted actor.

## What Changes

Decision: **fix** the existing owner callback and integrate its additive accepted-actor DATA, preserving independent consent/source checks. Customer retains its actual account/profile binding and checks the returned actor before any contact disclosure. Delivery owns the callback-only current duty/assignment/counterparty validation without any Customer RPC. The existing full physical execution authority remains unchanged.

## Impact

Owning issue: https://gitlab.xnata.com/gzdkh/services/DKH.CustomerService/-/issues/54. Customer source branch `fix/54-delivery-contact-callback-01a166de-20261026` is leased from main2497f8. Delivery's existing whole118 owner provides the additive generated `acting_actor_ref` tag9; no raw-wire client or fake package is permitted. The real self branch must equal the actual locked profile; the company branch must be the exact native `counterparty.party` authority. Recipient profile remains a separate target. Missing/legacy/service actor DATA denies.

Discovery: existing `authorize-delivery-contact` spec; Customer ownership/store and generated Delivery callback adapter; Delivery `CurrentExecutionDutyAuthority` and `CurrentOwnDutyReadAuthority`; genuine Delivery1.16 and Platform.Grpc.Common1.2.3; existing native persistence and generated callback tests. The external discovery receipt records current Customer/Delivery main searches. Existing archived Customer52/foreign journal work and dirty primary remain untouched.

Non-goals: no profile/grant activation, credentials, schema, Customer public contract, new business policy, PII probe, physical execution bypass, financial authority or original140 acceptance. DEV SDK receiving is source-only until genuine publication and ordinary delivery complete.
