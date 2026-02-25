---
description: Build verification before committing
globs: "**/*.{cs,csproj}"
---

# Build gating (MANDATORY)

Before EVERY commit in a .NET project:

1. Run `dotnet format --verify-no-changes` — STOP if it fails, fix with `dotnet format`
2. Run `dotnet build -c Release` — STOP if it fails
3. Run `dotnet test` — STOP if tests fail
4. Only then create the commit

**NEVER** commit code that has formatting violations, does not build, or has failing tests.
