# Tasks: DKH.CustomerService — In review

**Generated**: 2026-04-06 08:28 UTC
**Repository**: GZDKH/DKH.CustomerService
**Status filter**: In review
**Total tasks**: 2

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
### In review protocol (IMPORTANT)

For EACH task below, you MUST:

1. Read the issue and related PRs / commits
2. Verify existing implementation against the issue's acceptance criteria
3. If implementation is complete AND all quality gates pass:
   - Update status to Done on the project board
   - Close the issue if not already closed
4. If implementation is incomplete or broken:
   - Fix the gap
   - Run all blocking quality gates
   - Commit and push the fix
   - Leave status as In review for re-verification

## Tasks

---
### #43 fix(profiles): make firstName optional for Telegram users

**URL**: https://github.com/GZDKH/DKH.CustomerService/issues/43
**Assignees**: _unassigned_
**Labels**: bug
**Status**: In review

## Description

Telegram users may not have firstName. JWT tokens from token exchange may not contain the `given_name` claim. `CustomerProfileEntity.Create()` throws `ArgumentException` on empty firstName, breaking profile creation for all users.

## Acceptance Criteria
- [ ] `CustomerProfileEntity.Create()` accepts empty/null firstName without throwing
- [ ] `GetOrCreateProfileCommandValidator` treats firstName as optional (same pattern as LastName)
- [ ] Build and tests pass

---

### #44 feat(profiles): add is_premium and allows_write_to_pm to GetOrCreateProfileRequest

**URL**: https://github.com/GZDKH/DKH.CustomerService/issues/44
**Assignees**: _unassigned_
**Labels**: _none_
**Status**: In review

## Description
Proto GetOrCreateProfileRequest has 7 fields but TelegramUserModel has 8. Add:
- is_premium (bool) — field 8
- allows_write_to_pm (bool) — field 9

This is additive (minor version bump for Contracts).

Gateway already passes these in TelegramUserDto but can't send to CustomerService yet.

