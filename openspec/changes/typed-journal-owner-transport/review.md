# Critical boundary self-review

Reviewer: executor01a1245f. This is an author self-review of the documentation, not the independent parent review required for implementation admission. Source baseline Customer324b97e2; reviewed files are this new change and the compact discovery packet. No runtime/test/package pass is claimed.

| Risk found | Specification disposition | Remaining acceptance |
|---|---|---|
| Platform parses first X-Storefront-Id; token client ID does not authenticate gateway origin | D1 rejects these as standalone authority and requires principal-bound verified forwarding context; creation unavailable without producer | G1 actual authenticated producer/owner tests |
| Process-local cursor keys fail restart and sharing; shared generic purpose permits cross-use | D2 separates list/history protocol purpose and account/filter/entry binding;24h validity and persisted rotation window | G2 key/config/persistence admission and tests |
| Copy current source or hash of resolved mutable draft changes retries | D3 uses explicit immutable source revision/date/options intent and shared C2 receipt namespace; first execution validates source, replay returns saved destination without source-body access | G3 producer extension and transaction tests |
| Reading current header after old receipt returns newer private content | D4 projects the original immutable result consistently; no current-header mixing | RT03-P04/N09 |
| Reconstructing deleted body violates C2 deleted-serving semantics | P1 proposes NotFound for body replay after destination deletion; Delete Empty replay remains idempotent | Parent policy acceptance still pending |
| Blanket unknown-tag ban breaks additive clients; generated parser hides duplicate known oneof input | D5 distinguishes known invalid/ambiguous wire from benign future tags and names ingress evidence gate | G4 parser strategy plus raw-wire fixtures |
| Internal Product producer mistaken for Catalog authorization | D6 blocks every Product write/rebind/context/retaining-Copy path before core mutation; unchanged retained-target owner reads/edits separated | RT03-N06, no Product writer until EJ04 |
| Unqualified retry promise outlives the accepted24h receipt | D3/D4/spec explicitly limit stable replay to existing lifetime and bind trusted creation origin | RT03-P04/N09 and expiry fixtures |
| Candidate version or green docs CI mistaken for publish authorization | D7 keeps1.10.0 unchanged,1.11.0 unapproved and identifies main contract/image/deploy pipeline | Version confirmation and independent release admission |

Review verdict: bounded draft is internally coherent for parent review; G1–G4/P1 and runtime/version/release gates remain explicitly pending. No independent-review pass, RT03 completion, delivery status or task closure is asserted. Structural validator results are reported separately by exact commit/pipeline in the worker handoff.
