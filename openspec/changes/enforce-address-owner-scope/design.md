# Address owner boundary

Keep the existing authenticated self caller check and policy. Request identifiers select rows; they do not prove an address owner. The source join is active CustomerProfile(StorefrontId, UserId, !IsDeleted, AccountStatus.Active) to Address.CustomerId. Ordinary address operations additionally require !Address.IsDeleted. Explicit inactive list selection changes only address state, never profile/owner restrictions.

Do not change shared CustomerRepository semantics, the EF model/global filters, or introduce identity aliases. Restore and permanent delete have existing admin-only method policy and no self ownership selectors; keep their protocol and authorization boundary. Read/mutation results keep existing unavailable/not-found shapes.

The API retains the original request context, authenticated principal/claims, selected storefront and cancellation through awaited dispatch; a late caller/context change refuses disclosure. This is a request-local boundary, not a new credential validator, shared framework or distributed commit/instant database-revocation contract. A mutation may already have committed before a late disclosure refusal; no false rollback or blind retry is claimed.

Only Customer source/tests/spec/security evidence change. Delivery consumes the corrected ordinary owner data; no Delivery source, contact revision, place identity or financial authority is manufactured.
