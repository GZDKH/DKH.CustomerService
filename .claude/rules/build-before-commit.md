---
description: Build verification before committing
globs: "**/*.{cs,csproj}"
---

# Build gating (MANDATORY)

Before EVERY commit in a .NET project:

1. Run `dotnet build -c Release` — STOP if it fails
2. Run `dotnet test` — STOP if tests fail
3. Only then create the commit

**NEVER** commit code that does not build or has failing tests.
