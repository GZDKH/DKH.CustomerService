# Historical recovery: private collection test branch

The local branch `feat/6-private-collection-isolation` at
`d32934a264813dfa6a77848c0bffec5d52586945` is retained as a parent of this
recovery commit. The private collection test and its original plan were
subsequently included byte for byte in commit `e4541bc`, which is in main.

The saved branch also changed two central package versions. Those changes were
not part of `e4541bc`; this recovery keeps the package versions from current
main. The branch remains available in the parent history for inspection.

A squash merge would omit the historical parent. Preserve the original commit
separately before squashing or deleting the recovery source branch.

## Additional local test snapshot

The untracked file
`tests/DKH.CustomerService.IntegrationTests/Integration/Application/AdminCustomerOlacTests.cs`
is preserved byte for byte as `docs/recovery/AdminCustomerOlacTests.cs.txt`.
Its SHA-256 is `ad1b9f11503ac986c9835c29e3b7bdf4b90e22454ab4688d4de039bc47009a5d`.
This historical source is not an executable test or evidence that the
associated authorization behavior is implemented. The original file remains in
the primary checkout.

## Additional historical stashes

- `9bed2889a3e95682419adde60f6bee5985310f20` preserves an older Codex rules
  migration, including its index and untracked-file parents.
- `8caea2bc89c8e190c3b935c3c37d3853605a0c6b` preserves two untracked prompt
  documents and their original stash parents.

Both snapshots remain historical parents only. Their rules and prompts are not
applied to the current checkout and are not current operating guidance.
