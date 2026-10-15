# Private delivery contact authority

Customer52 accompanies original plan row83 (V6.7) / DeliveryService#2. It does not complete whole V6.7.

The additive `proto.customer.api.delivery_contact.v2.DeliveryContactService` seals an owned current address as a real persisted observation, resolves/revalidates an opaque self liability reference, records/revokes recipient consent and defines least-data recipient resolution. Source version1.11.0 is additive to the genuine1.10 API; normal main CI owns publication.

The actor comes from the existing validated personal Bearer issuer/sub capture. Global account ID, linked identity row, storefront membership row, CustomerProfile ID and JWT subject remain distinct. `owner_profile_binding` explicitly means CustomerProfile.Id. It is not evidence that a Delivery group participant was created from that profile.

Seal is explicit current observation, not reconstruction of historical revision1. Address content is not copied into history. Current source/account/profile linkage must still match; edits, deletion, blocked ownership or newer seals invalidate old availability. Retained reference metadata never resurrects deleted address content.

`ResolveLiabilityContact` returns no contact content. `verification_expires_at` describes a short current-state observation, not the underlying authority lifetime or a Delivery duty deadline. `RevalidateLiabilityContact` repeats current checks using the original operation and verification ID; no retry extends expiry.

Recipient consent binds exact group/allocation/plan/scope/choice UUID/duty reference and local-shipment purpose. It is recipient intent, not a grant. The current production authorizer deliberately returns `AUTHORITY_UNAVAILABLE`: Delivery has no published authenticated callback or genuine group participant identity producer yet. Positive access requires that real owner boundary, forwarded SAME human Bearer, echoed exact typed scope, actual profile binding and current recipient consent. No callback, synthetic adapter, token, registry or group identity is invented here.

When the genuine adapter is supplied, output is restricted to recipient name, postal address, carrier-required phone, session ID, expiry and scope revision. It excludes email/billing/account/customer/order fields. Revocation deduplication is owner-scoped: a reused operation with another consent/expected revision/body conflicts, backed by a partial unique database index. All leases and consent expire within five minutes or earlier genuine limits; replay revalidates current state and returns the original unextended result. There is no cross-service atomic revocation claim.

The three new tables retain references/hashes and operation results. PostgreSQL guards make history immutable except explicit consent revocation. Populated downgrade locks all three tables before checking emptiness and retains the locks through destructive DDL, raising55000 if history exists. Statement-level TRUNCATE is refused on each retained table. Empty downgrade affects only these objects. Existing journal behavior/data and address permissions are unchanged.

CPU and compiled native tests are distinct evidence. Local native execution is not performed; ordinary CI must supply actual PostgreSQL results, then normal main/runtime delivery evidence is required. Whole shipping/V6.7 remains OPEN pending the real Delivery callback and participant owner mapping.
