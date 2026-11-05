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
