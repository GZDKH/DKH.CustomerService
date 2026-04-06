# Tasks: DKH.CustomerService — Backlog

**Generated**: 2026-04-06 08:28 UTC
**Repository**: GZDKH/DKH.CustomerService
**Status filter**: Backlog
**Total tasks**: 1

## MANDATORY rules

> **All paths below are relative to monorepo root** (`GZDKH/`).

Before executing ANY task, you MUST read and follow:

1. **Project rules**: `CLAUDE.md` in this project's root directory
2. **Baseline rules**: `AGENTS.md` and `CLAUDE.md` in monorepo root
3. **Shared rules** (MANDATORY):
   - `libraries/DKH.Architecture/AGENTS.md`
   - `libraries/DKH.Architecture/.claude/shared-rules/commits.md` — Conventional Commits, NO `Co-Authored-By`
   - `libraries/DKH.Architecture/.claude/shared-rules/security.md` — never commit secrets
4. **Technology rules** (choose applicable):
   - .NET: `libraries/DKH.Architecture/docs/agents-dotnet.md`
   - Next.js/React: `libraries/DKH.Architecture/docs/agents-nextjs.md`
   - Platform: `libraries/DKH.Architecture/docs/agents-platform.md`

## Execution protocol

Process tasks in order (P0 → P1 → P2 → unassigned). For each task:

1. Read the task description and URL
2. Apply relevant skills (systematic-debugging, test-driven-development)
3. Implement the minimal change to satisfy the issue
4. Run blocking quality gates:
   - .NET: `dotnet build -c Release && dotnet test`
   - Next.js: `pnpm build`
5. Commit per Conventional Commits: `<type>(<scope>): <summary>` — NO `Co-Authored-By`
6. Push to remote
7. Update task status on board via `gh project item-edit`

## Tasks

---
### #47 feat(collection): add ProductCollectionItemEntity for personal product shelf

**URL**: https://github.com/GZDKH/DKH.CustomerService/issues/47
**Assignees**: _unassigned_
**Labels**: _none_
**Status**: Backlog

## Description
Add ProductCollectionItemEntity to track customer's product collection (own/using/finished/wishlist). Replaces the tea-specific 'Tea Stash' concept with a universal product collection.

## Acceptance Criteria
- [ ] ProductCollectionItemEntity with CustomerId, ProductId, ProductSkuId, Status, Notes, Rating, AddedAt
- [ ] ProductCollectionStatus enum: Own, Using, Finished, Wishlist
- [ ] EF configuration + migration
- [ ] gRPC service: AddToCollection, UpdateStatus, RemoveFromCollection, ListCollection
- [ ] Proto contracts

Part of: structured reviews plan

