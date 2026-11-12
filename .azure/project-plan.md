# Project Plan

**Status**: Awaiting Integration
**Created**: 2026-11-12
**Mode**: NEW

---

## 1. Project Overview

**Goal**: Build a remotely hosted, HTTPS-accessible Model Context Protocol (MCP) server that can also run locally for development and client compatibility testing. It exposes the requested starter `echo` and `get_current_time` tools; these examples can be replaced with domain-specific tools later. The project is designed so that every module is independently testable.

**App Type**: API only

**API Login**: Yes

**Mode**: NEW

**Deployment Plan**: No deployment plan found

---

## 2. Backend - ASP.NET Core and Azure App Service

| Component | Technology |
|-----------|-----------|
| **Language** | C# (.NET) |
| **Runtime** | .NET |
| **Framework** | ASP.NET Core and Azure App Service |
| **Package Manager** | dotnet (NuGet) |
| **Test Runner** | xUnit |
| **Mocking Library** | NSubstitute |
| **Test Command** | `dotnet test` |
| **Orchestration** | docker-compose |

ASP.NET Core on Azure App Service is the confirmed custom framework: build the server as an ASP.NET Core minimal-hosting app on .NET 10 and deploy it to Azure App Service over HTTPS. Implement MCP protocol handling with the official C# SDK rather than hand-rolling JSON-RPC or transport behavior. Expose a single Streamable HTTP MCP endpoint at `/mcp`; the SDK handles initialization, tool discovery/calls, notifications, and session behavior.

Register two starter tools:

- `echo`: accept a text value and return it unchanged.
- `get_current_time`: return the current UTC time in ISO 8601 format.

Do not expose resources or prompts in the initial version. Keep tool handlers independently testable, validate tool inputs, honor cancellation, and avoid logging credentials or full sensitive request contents.

API Login is required: clients must sign in before they can list or call tools. The login has to work for Copilot Studio, Azure AI Foundry, and GitHub Copilot; test each client separately against the deployed HTTPS endpoint before declaring compatibility. The scaffold step selects and implements the login mechanism.

For local development, run the same app and transport on localhost. Automated tests may substitute an isolated test login; do not add a production login bypass. Keep configuration in environment/app settings and secrets out of source control.

---

## 3. Services Required

| Azure Service | Role in App | Environment Variable | Default Value (Local) | Classification |
|---------------|------------|---------------------|----------------------|----------------|
| Azure App Service | Hosts the ASP.NET Core MCP server and terminates public HTTPS traffic | `ASPNETCORE_URLS` | `http://localhost:5000` | Essential |
| Application Insights | Captures operational logs, request failures, and availability telemetry | `APPLICATIONINSIGHTS_CONNECTION_STRING` | Unset; console logging remains available | Enhancement |

No application datastore is required. Do not add a database, storage account, or Azure Functions storage dependency unless future tools require persistence or file handling.

---

## 4. Prerequisites

### Run

| Tool | Service(s) | Installed | Version |
|------|------------|-----------|---------|
| .NET SDK | mcp-server | ✅ | 10.0.101 |

### Debug

| Tool | Service(s) | Installed | Version |
|------|------------|-----------|---------|

No project-specific emulator, container runtime, browser, or Azure Functions VS Code extension is required: the server has no emulator-backed Azure dependencies, frontend, or Functions service. Use the ASP.NET Core launch profile and MCP Inspector or a configured MCP client for local protocol debugging. Verify any optional tools before use.

---

## 5. Project Structure

```
MCPServer2.0/
├── .azure/
│   └── project-plan.md
├── .gitignore
├── global.json
├── MCPServer.sln
├── src/
│   └── MCPServer/
│       ├── MCPServer.csproj
│       ├── Program.cs
│       ├── Mcp/
│       │   ├── McpServerRegistration.cs
│       │   └── Tools/
│       │       ├── EchoTool.cs
│       │       └── CurrentTimeTool.cs
│       └── Health/
│           └── HealthEndpoints.cs
└── tests/
    └── MCPServer.Tests/
        ├── MCPServer.Tests.csproj
        ├── Mcp/
        └── Health/
```

Keep the server stateless. The scaffold step adds the files and tests for the API login it implements. Store local-only configuration in user secrets or ignored settings files; configure Azure App Service settings separately. Pin the .NET SDK through `global.json` and resolve current stable NuGet package versions during scaffolding.

---

## 6. Route Definitions

| # | Method | Path | Description | Request Body | Response Body | Status Codes |
|---|--------|------|-------------|-------------|--------------|-------------|
| 1 | POST | `/mcp` | Streamable HTTP MCP requests, including initialization and tool calls | MCP JSON-RPC message | MCP JSON-RPC response or protocol notification response | 200, 202, 400 |
| 2 | GET | `/mcp` | Optional server-initiated Streamable HTTP stream, as supported by the SDK | MCP session headers; no body | MCP event stream | 200, 405 |
| 3 | DELETE | `/mcp` | End an MCP session, as supported by the SDK | MCP session headers; no body | Empty protocol response | 200, 405 |
| 4 | GET | `/health/live` | Process liveness check; returns no operational details | — | `{ "status": "ok" }` | 200 |
| 5 | GET | `/health/ready` | Readiness check for the HTTP server and required configuration | — | `{ "status": "ok" }` or generic unavailable status | 200, 503 |

The MCP SDK owns protocol semantics for `/mcp`; enable only the HTTP methods and session behavior it implements. Keep readiness responses free of secrets and internal exception details.

---

## 7. Next Steps

1. Run **azure-project-scaffold** to execute this plan.
2. Run **azure-project-integrate** to smoke-test the backend endpoints and verify its local runtime behavior; no database migrations or frontend wiring are required for this API-only project.
3. Run **azure-debug-plan** → **azure-debug-generate** for local run configuration and API/MCP client tests.
4. Run the **azure-deploy** agent when ready; it uses **azure-app-onboard** for architecture, cost estimation, IaC generation, provisioning, and health verification.
