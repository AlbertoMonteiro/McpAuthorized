# McpAuthorized – MCP Server

This is the ASP.NET Core MCP server component of the **McpAuthorized** PoC.  
For a full project overview, architecture, and testing instructions see the [root README](../README.md).

## What it does

- Exposes an MCP endpoint at `POST /` using **Streamable HTTP transport**.
- Validates incoming requests with **JWT Bearer tokens** issued by a Keycloak `api` realm.
- Registers the `get_random_number` tool (see `Tools/RandomNumberTools.cs`).

## Key files

| File | Purpose |
|---|---|
| `Program.cs` | Authentication setup (JWT Bearer + MCP OAuth metadata) and MCP registration |
| `Tools/RandomNumberTools.cs` | Example MCP tool that returns a random integer |
| `McpAuthorized.http` | HTTP test file – token → session → tool call flow |

## Connecting from an IDE

Because the server requires a valid Bearer token, direct IDE connections (without a token) will receive a `401 Unauthorized` challenge. Use the `.http` file or configure your MCP client to supply a token.

```json
{
  "servers": {
    "McpAuthorized": {
      "type": "http",
      "url": "https://localhost:5092"
    }
  }
}
```

Refer to the VS Code or Visual Studio documentation for more information:

- [Use MCP servers in VS Code (Preview)](https://code.visualstudio.com/docs/copilot/chat/mcp-servers)
- [Use MCP servers in Visual Studio (Preview)](https://learn.microsoft.com/visualstudio/ide/mcp-servers)

## Known issues

1. When using VS Code, connecting to `https://localhost:5092` may fail due to a self-signed developer certificate.
   - Connecting with `http://localhost:6102` succeeds.
   - See [microsoft/vscode#248170](https://github.com/microsoft/vscode/issues/248170) for more information.

## More information

- [ModelContextProtocol.AspNetCore NuGet package](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore)
- [MCP C# SDK](https://modelcontextprotocol.github.io/csharp-sdk)
- [Official MCP Documentation](https://modelcontextprotocol.io/)
