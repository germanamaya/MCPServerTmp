# Integration Plan

Hand-off brief for `azure-project-integrate`. Source of truth for requirements: [project-plan.md](./project-plan.md).

**Project type**: API only. No frontend, no database.

## Backend

| Item | Value |
|------|-------|
| Project folder | `src/MCPServer` (ASP.NET Core minimal hosting, `net10.0`) |
| Solution | `MCPServer.sln` (repo root) |
| Build | `dotnet build MCPServer.sln` |
| Test | `dotnet test MCPServer.sln` (62 tests, xUnit + NSubstitute + Shouldly) |
| Run | `dotnet run --project src/MCPServer/MCPServer.csproj` (uses the `http` launch profile, environment `Development`) |
| URL / port | `http://localhost:5000` |
| Health | `GET /health/live` (liveness), `GET /health/ready` (readiness) |
| Request samples | `src/MCPServer/MCPServer.http` |
| Settings reference | `.env.example` (documentation only; ASP.NET Core does not load it) |

Port 5000 is also held by macOS AirPlay Receiver on the wildcard address. Kestrel binds `127.0.0.1` and `::1` specifically, and requests to `localhost:5000` reach the server.

## Frontend

None. Skip frontend wiring, the API seam swap, and mock-file deletion.

## Database

None. Do not create migrations or seed data. The server is stateless.

## API routes

| Method | Path | Auth | Expected |
|--------|------|------|----------|
| GET | `/health/live` | Anonymous | 200 `{"status":"ok"}` |
| GET | `/health/ready` | Anonymous | 200 `{"status":"ok"}`; 503 `{"status":"unavailable"}` when sign-in settings are missing or the host is starting/stopping |
| POST | `/mcp` | Bearer token | MCP Streamable HTTP (JSON-RPC). 401 without a valid token, 403 without an accepted scope or role |
| GET | `/mcp` | Bearer token | 405 (stateless server; no server-initiated stream) |
| DELETE | `/mcp` | Bearer token | 405 (stateless server; no sessions) |
| GET | `/auth/me` | Bearer token | 200 `{ id, name, scopes, roles }` |
| GET | `/.well-known/oauth-protected-resource/mcp` | Anonymous | 200 OAuth protected-resource metadata (served by the MCP SDK) |
| GET | `/openapi/v1.json` | Anonymous | 200, `Development` environment only |

Any other path returns 401 without a token: authorization is required by default.

MCP tools: `echo` (argument `text`, max 10,000 characters) and `get_current_time` (no arguments, UTC ISO 8601). No resources or prompts.

Error shape for non-MCP responses: `{ "error": { "code", "message", "details" } }`.

## API login

- Mechanism: OAuth 2.0 bearer tokens (JWT) validated against an OpenID Connect authority; intended for Microsoft Entra ID. Registration and sign-in happen at the authority, so the server has no registration or login endpoint and stores no accounts.
- Files: `src/MCPServer/Auth/` (`AuthSettings`, `AuthenticationRegistration`, `ScopeOrRoleAuthorization`, `CurrentUser`, `AuthEndpoints`, `TokenClaims`).
- Policy `McpAccess` (also the fallback policy): authenticated, plus one configured scope (`scp`/`scope` claim) or app role (`roles` claim) when any are configured.
- Required settings: `Authentication__Authority` (HTTPS, single tenant), `Authentication__Audiences__0`. Optional: `Authentication__Scopes__N`, `Authentication__Roles__N`.
- No signing secret is stored: signing keys come from the authority's metadata.
- Missing or invalid settings: the server still starts, logs an error, rejects every token (401), and reports readiness 503. There is no login bypass in any environment.
- Tests sign tokens with a test key trusted through the real validation pipeline: `tests/MCPServer.Tests/Support/`.

## Smoke test without a tenant

Placeholder settings are enough to start the server and check everything except a successful signed-in call:

```
Authentication__Authority=https://login.microsoftonline.com/<any-tenant-guid>/v2.0
Authentication__Audiences__0=<any-guid>
```

Expected: `/health/live` 200, `/health/ready` 200, `POST /mcp` 401 with `WWW-Authenticate: Bearer resource_metadata="…/.well-known/oauth-protected-resource/mcp"`. Without these settings `/health/ready` returns 503 by design.

A signed-in MCP call needs a real access token from the configured authority. The in-memory tests in `tests/MCPServer.Tests/Mcp/McpEndpointTests.cs` cover that path with a real MCP client.

## Services

| Service | Classification | Setting | Notes |
|---------|----------------|---------|-------|
| Azure App Service | Essential | `ASPNETCORE_URLS` | Hosting. Set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` on Linux so the sign-in metadata URL uses `https://` |
| Application Insights | Enhancement | `APPLICATIONINSIGHTS_CONNECTION_STRING` | Unset or invalid: console logging only, startup is unaffected |

No Azure SDK data clients are used, so there are no emulator or managed-identity client providers.

## Shared types

None. Single service; contracts live in `src/MCPServer`.

## Not yet verified

- A signed-in call against a real Entra ID tenant.
- Sign-in from Copilot Studio, Azure AI Foundry, and GitHub Copilot against a deployed HTTPS endpoint (the plan requires testing each separately).
