# McpAuthorized

A proof-of-concept (PoC) demonstrating how to build a **C# MCP (Model Context Protocol) server** with **HTTP transport** and **authentication**, orchestrated by **.NET Aspire** and secured by **Keycloak**.

## Overview

The [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) is an open standard that allows AI models to securely connect to external tools and data sources. This project shows how to:

- Host an MCP server as an ASP.NET Core web application using HTTP (Streamable HTTP) transport.
- Protect MCP endpoints with JWT Bearer authentication issued by **Keycloak**.
- Orchestrate all services (MCP server + Keycloak) with **.NET Aspire**.
- Import a pre-configured Keycloak **Realm** to make testing fast and repeatable.
- Exercise the full authentication and tool-call flow using a ready-made **`.http` file**.

## Architecture

```
┌────────────────────────────────────────────────────────────────┐
│  .NET Aspire AppHost                                           │
│                                                                │
│  ┌─────────────────────┐      ┌──────────────────────────┐    │
│  │  Keycloak (Docker)  │◄────►│  McpAuthorized (ASP.NET) │    │
│  │  realm: api         │      │  /  → MCP endpoint       │    │
│  │  client: mcp-user   │      │  JWT Bearer validation   │    │
│  └─────────────────────┘      └──────────────────────────┘    │
└────────────────────────────────────────────────────────────────┘
```

1. **Keycloak** is started as a persistent Docker container with the `api` realm imported automatically from `McpAuthorized.AppHost/Realms/api-realm.json`.
2. **McpAuthorized** (the MCP server) reads the Keycloak URL from its environment and validates every incoming request against a JWT token issued by the `api` realm.
3. The MCP server exposes a single tool (`get_random_number`) through the `POST /` endpoint.

## Prerequisites

| Requirement | Version |
|---|---|
| [.NET SDK](https://dot.net/download) | 10.0+ |
| [.NET Aspire workload](https://learn.microsoft.com/dotnet/aspire/fundamentals/setup-tooling) | 9.x / 13.x preview |
| [Docker](https://www.docker.com/products/docker-desktop/) | any recent version |

Install the Aspire workload if you haven't already:

```bash
dotnet workload install aspire
```

## Running the Project

```bash
cd McpAuthorized.AppHost
dotnet run
```

Aspire will:
1. Pull and start a **Keycloak** container on port `8080`, automatically importing the `api` realm.
2. Start the **McpAuthorized** web server (by default at `https://localhost:5092`).
3. Wait for Keycloak to be healthy before starting the MCP server.

Open the Aspire Dashboard (URL printed in the console) to monitor both services.

## Keycloak Configuration

> ⚠️ **Security notice:** `api-realm.json` contains hardcoded client credentials intended for **local development only**. Never deploy this realm file to a shared or production environment. Regenerate all secrets before using this configuration outside your local machine.

The `api` realm is imported from [`McpAuthorized.AppHost/Realms/api-realm.json`](McpAuthorized.AppHost/Realms/api-realm.json). It comes pre-configured with:

| Setting | Value |
|---|---|
| Realm | `api` |
| Client ID | `mcp-user` |
| Client Secret | `7lrl6I10qLM5LaUUoLSI2KfBJoIsBWRg` |
| Grant type | `client_credentials` |
| Scope | `mcp:tools` |

The MCP server is configured to accept tokens from:

```
http://<keycloak-host>/realms/api
```

and validates that the `aud` (audience) claim contains `account`.

## Authentication Flow

The MCP server uses the **MCP OAuth 2.0 protected resource** pattern:

1. The client requests a token from Keycloak using the `client_credentials` grant.
2. The token is sent as a `Bearer` token in the `Authorization` header on every request to the MCP server.
3. The server validates the token (issuer, audience, lifetime, signature) via standard `JwtBearer` middleware.
4. If validation succeeds, the request is forwarded to the MCP pipeline.

## Testing with the HTTP File

[`McpAuthorized/McpAuthorized.http`](McpAuthorized/McpAuthorized.http) contains a ready-made sequence of requests that exercises the full flow. You can run them directly in **Visual Studio**, **VS Code** (with the REST Client extension), or **JetBrains Rider**.

### Step 1 – Obtain a Token

```http
POST http://localhost:8080/realms/api/protocol/openid-connect/token
Content-Type: application/x-www-form-urlencoded

client_id=mcp-user&client_secret=7lrl6I10qLM5LaUUoLSI2KfBJoIsBWRg&grant_type=client_credentials&scope=mcp:tools
```

The response body contains `access_token`. The `.http` file stores it as `{{login.response.body.$.access_token}}` for use in subsequent requests.

### Step 2 – Start an MCP Session

```http
POST https://localhost:5092/
Authorization: Bearer {{login.response.body.$.access_token}}
MCP-Protocol-Version: 2025-11-25
Content-Type: application/json

{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "initialize",
  ...
}
```

The response header `Mcp-Session-Id` contains the session identifier used in subsequent calls.

### Step 3 – Call the MCP Tool

```http
POST https://localhost:5092/
Authorization: Bearer {{login.response.body.$.access_token}}
MCP-Session-Id: {{NewSession.response.headers.Mcp-Session-Id}}
Content-Type: application/json

{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": { "name": "get_random_number" }
}
```

The server returns a random integer between 0 and 99 (default bounds).

## Project Structure

```
McpAuthorized.sln
│
├── McpAuthorized/                   # MCP server (ASP.NET Core)
│   ├── Program.cs                   # Authentication & MCP setup
│   ├── Tools/
│   │   └── RandomNumberTools.cs     # Example MCP tool
│   └── McpAuthorized.http           # Test HTTP file
│
├── McpAuthorized.AppHost/           # .NET Aspire orchestration
│   ├── AppHost.cs                   # Defines Keycloak + MCP server
│   └── Realms/
│       └── api-realm.json           # Keycloak realm import
│
└── McpAuthorized.ServiceDefaults/   # Shared Aspire service defaults
    └── Extensions.cs
```

## Key Packages

| Package | Purpose |
|---|---|
| [`ModelContextProtocol.AspNetCore`](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore) | MCP server SDK for ASP.NET Core (HTTP transport) |
| [`Aspire.Keycloak.Authentication`](https://www.nuget.org/packages/Aspire.Keycloak.Authentication) | Aspire integration for Keycloak JWT authentication |
| [`Aspire.Hosting.Keycloak`](https://www.nuget.org/packages/Aspire.Hosting.Keycloak) | Aspire hosting extension to run Keycloak in Docker |

## Further Reading

- [Model Context Protocol – Official Documentation](https://modelcontextprotocol.io/)
- [MCP C# SDK](https://modelcontextprotocol.github.io/csharp-sdk)
- [.NET Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/)
- [Keycloak Documentation](https://www.keycloak.org/documentation)
