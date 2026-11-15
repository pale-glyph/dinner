# Dinner Repository Guidance

## Service architecture

Services use hexagonal architecture (ports and adapters). Keep business behavior independent of HTTP, databases, configuration providers, and external services.

- `Domain/` contains business entities, value objects, state transitions, and invariants. It must not depend on ASP.NET Core, Npgsql, HTTP clients, or infrastructure implementations.
- `Application/` contains use cases and defines the ports it needs, such as repositories, external-service clients, clocks, and signature validators. Application code depends only on domain code and these abstractions.
- `Infrastructure/` contains outbound adapters: database repositories, HTTP clients, message brokers, cryptography implementations, and configuration-backed integrations. It implements application ports.
- `Web/` (or another transport-specific adapter) contains endpoint mapping, request/response DTOs, input validation, and HTTP-status translation. It calls application use cases; it must not contain business workflows or persistence code.
- `Program.cs` is the composition root. Keep it limited to configuration binding, dependency injection, middleware, service startup, and endpoint registration.

Dependencies must point inward:

```text
Web adapters -> Application -> Domain
Infrastructure adapters -> Application -> Domain
Program.cs -> all layers (composition only)
```

Do not reference an infrastructure implementation from application or domain code. Introduce or extend an application port instead.

## C# conventions

- Use file-scoped namespaces and nullable reference types.
- Prefer descriptive types and methods over utility classes or static workflow functions.
- Pass `CancellationToken` through asynchronous application, repository, and external-client operations.
- Keep public contracts explicit and immutable where practical (`record` types are appropriate for transport and result contracts).
- Use `dotnet format` and build the affected project before handing off changes.

## API and persistence changes

- Preserve existing public endpoint behavior unless the task explicitly requests an API change.
- Keep HTTP-specific status handling in the transport adapter.
- Make persistence changes backward compatible where possible, and keep schema initialization or migrations inside infrastructure.

## React frontend organization

- Break UI work into focused, reasonably sized components. Do not keep unrelated views, hooks, data access, and presentation logic in one file.
- Put code in the directory that describes its role, such as `components/`, `hooks/`, `lib/`, `types/`, `features/`, or a feature-local equivalent.
- Keep reusable components and hooks separate from feature-specific code. Co-locate code when it is only used by one feature or route.
- Import application code through the configured `@/` path aliases (for example, `@/components/...` and `@/hooks/...`) instead of long relative paths.
