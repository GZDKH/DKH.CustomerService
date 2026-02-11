---
description: Do not duplicate DKH.Platform functionality
globs: "**/*.cs"
---

# No infrastructure duplication (MANDATORY)

Before writing ANY infrastructure code:

1. **Check DKH.Platform** for existing abstractions first
2. Use `AddPlatform*()` extension methods — **NEVER** manual DI registration
3. Use `Platform.CreateWeb(args)` or `Platform.Create(args)` entry point

**Anti-patterns** (NEVER do these):
- `services.AddDbContext<T>()` directly — use `AddPlatformPostgreSql<T>()`
- `services.AddMediatR()` directly — use `AddPlatformMessagingWithMediatR()`
- `services.AddSwaggerGen()` — use `AddPlatformRestfulApi()`
- `services.AddAuthentication().AddJwtBearer()` — use `AddPlatformKeycloakAuth()`
- Manual `Serilog.Log.Logger` setup — use `AddPlatformLogging()`
