# McpAuthorized

A proof-of-concept (PoC) demonstrating how to build a **C# MCP (Model Context Protocol) server** with **HTTP transport** and **authentication**, orchestrated by **.NET Aspire** and secured by **Keycloak**.

## Overview

The [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) is an open standard that allows AI models to securely connect to external tools and data sources. This project shows how to:

- Host an MCP server as an ASP.NET Core web application using HTTP (Streamable HTTP) transport.
- Protect MCP endpoints with JWT Bearer authentication issued by **Keycloak**.
- Orchestrate all services (MCP server + Keycloak + MCP Inspector) with **.NET Aspire**.
- Import a pre-configured Keycloak **Realm** to make testing fast and repeatable.
- Exercise the full authentication and tool-call flow using a ready-made **`.http` file** or the built-in **MCP Inspector**.

## Architecture

```
┌────────────────────────────────────────────────────────────────────────┐
│  .NET Aspire AppHost                                                   │
│                                                                        │
│  ┌─────────────────────┐      ┌──────────────────────────┐            │
│  │  Keycloak (Docker)  │◄────►│  McpAuthorized (ASP.NET) │            │
│  │  realm: api         │      │  /  → MCP endpoint       │            │
│  │  client: mcp-user   │      │  JWT Bearer validation   │            │
│  └─────────────────────┘      └────────────┬─────────────┘            │
│                                            │                           │
│                               ┌────────────▼─────────────┐            │
│                               │     MCP Inspector        │            │
│                               │  Browser-based MCP tester│            │
│                               └──────────────────────────┘            │
└────────────────────────────────────────────────────────────────────────┘
```

1. **Keycloak** is started as a persistent Docker container with the `api` realm imported automatically from `McpAuthorized.AppHost/Realms/api-realm.json`.
2. **McpAuthorized** (the MCP server) reads the Keycloak URL from its environment and validates every incoming request against a JWT token issued by the `api` realm.
3. The MCP server exposes a single tool (`get_random_number`) through the `POST /` endpoint.
4. **MCP Inspector** is a browser-based tool that lets you interactively explore and test the MCP server, as an alternative to the `.http` file.

## Prerequisites

| Requirement | Version |
|---|---|
| [.NET SDK](https://dot.net/download) | 10.0+ |
| [Aspire CLI](https://learn.microsoft.com/dotnet/aspire/fundamentals/aspire-sdk-dotnet-cli) | latest |
| [Docker](https://www.docker.com/products/docker-desktop/) | any recent version |

Install the Aspire CLI if you haven't already:

```bash
dotnet tool install -g aspire
```

## Running the Project

```bash
cd McpAuthorized.AppHost
aspire run
```

Aspire will:
1. Pull and start a **Keycloak** container on port `8080`, automatically importing the `api` realm.
2. Start the **McpAuthorized** web server at `https://mcpauthorized.dev.localhost:5092`.
3. Start the **MCP Inspector** and connect it to the MCP server.
4. Wait for Keycloak to be healthy before starting the MCP server.

Open the Aspire Dashboard (URL printed in the console) to monitor all services.

> **Note on `.dev.localhost` hostnames:** On Linux and macOS, subdomains of `localhost` (like `mcpauthorized.dev.localhost`) resolve to `127.0.0.1` automatically. On Windows, you may need to add entries to your `hosts` file:
> ```
> 127.0.0.1  mcpauthorized.dev.localhost
> ```

## Keycloak as an MCP Authorization Server

This PoC follows the [Keycloak MCP Authorization Server](https://www.keycloak.org/securing-apps/mcp-authz-server) pattern, which aligns with the MCP specification's OAuth 2.0 security model.

### How MCP Authorization Works

The MCP specification defines a security model based on **OAuth 2.0 Protected Resources** (RFC 9728). Here is the full flow:

```
┌──────────┐     1. Discover AS      ┌─────────────┐
│  MCP     │ ───────────────────────►│  MCP Server │
│  Client  │◄─── authorization_      │  (this PoC) │
│          │     servers: [Keycloak] └──────┬──────┘
│          │                                │ 2. Validate JWT
│          │     3. Request token    ┌──────▼──────┐
│          │ ───────────────────────►│  Keycloak   │
│          │◄─── access_token        │  (OAuth AS) │
│          │                         └─────────────┘
│          │     4. Call MCP tool
│          │ ───── Bearer <token> ──►  MCP Server
└──────────┘                         responds with tool result
```

#### Step 1 – Protected Resource Metadata Discovery

MCP clients first call the MCP server's protected resource metadata endpoint:

```
GET /.well-known/oauth-protected-resource
```

The server responds with:

```json
{
  "resource": "account",
  "authorization_servers": ["http://localhost:8080/realms/api"],
  "scopes_supported": ["mcp:tools"],
  "resource_documentation": "https://docs.example.com/api/weather"
}
```

This tells MCP clients which Keycloak realm issues valid tokens for this server.

#### Step 2 – Obtain an Access Token from Keycloak

MCP clients authenticate against Keycloak using OAuth 2.0. This PoC uses the `client_credentials` grant (machine-to-machine):

```http
POST http://localhost:8080/realms/api/protocol/openid-connect/token
Content-Type: application/x-www-form-urlencoded

client_id=mcp-user&client_secret=7lrl6I10qLM5LaUUoLSI2KfBJoIsBWRg&grant_type=client_credentials&scope=mcp:tools
```

For interactive use cases (user-delegated access), Keycloak also supports `authorization_code` with **PKCE** (Proof Key for Code Exchange), which is the recommended flow for public clients like MCP desktop agents.

#### Step 3 – Call the MCP Server

The access token is sent as a `Bearer` token on every MCP request:

```http
POST https://mcpauthorized.dev.localhost:5092/
Authorization: Bearer <access_token>
MCP-Protocol-Version: 2025-11-25
Content-Type: application/json
```

#### Step 4 – Token Validation

The MCP server validates each JWT against Keycloak's OIDC discovery document:

| Claim | Expected value |
|---|---|
| `iss` (issuer) | `http://<keycloak-host>/realms/api` |
| `aud` (audience) | `account` |
| `scope` | contains `mcp:tools` |
| Signature | verified against Keycloak's JWKS |

### Keycloak Configuration

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

### Dynamic Client Registration (DCR)

In production scenarios, Keycloak supports **Dynamic Client Registration** (RFC 7591), which allows MCP clients to register themselves automatically without manual configuration. A Keycloak admin creates an initial registration token, and MCP clients use it to register and receive their own `client_id` and `client_secret`.

## Testing with MCP Inspector

The MCP Inspector is a browser-based interactive tool included in the Aspire dashboard. When you run the project, the Inspector is automatically connected to the MCP server.

From the Aspire dashboard, click the **MCP Inspector** link to:
- Browse the available MCP tools
- Call tools interactively
- Inspect request and response payloads

> MCP Inspector handles the authentication flow automatically using the configuration provided by the MCP server's Protected Resource Metadata.

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
POST https://mcpauthorized.dev.localhost:5092/
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
POST https://mcpauthorized.dev.localhost:5092/
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
│   ├── AppHost.cs                   # Defines Keycloak + MCP server + MCP Inspector
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
| [`CommunityToolkit.Aspire.Hosting.McpInspector`](https://www.nuget.org/packages/CommunityToolkit.Aspire.Hosting.McpInspector) | Aspire hosting extension for the MCP Inspector browser tool |

## Further Reading

- [Model Context Protocol – Official Documentation](https://modelcontextprotocol.io/)
- [MCP C# SDK](https://modelcontextprotocol.github.io/csharp-sdk)
- [Keycloak MCP Authorization Server](https://www.keycloak.org/securing-apps/mcp-authz-server)
- [.NET Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/)
- [Keycloak Documentation](https://www.keycloak.org/documentation)
- [OAuth 2.0 Protected Resource Metadata (RFC 9728)](https://datatracker.ietf.org/doc/html/rfc9728)
- [OAuth 2.0 Dynamic Client Registration (RFC 7591)](https://datatracker.ietf.org/doc/html/rfc7591)
