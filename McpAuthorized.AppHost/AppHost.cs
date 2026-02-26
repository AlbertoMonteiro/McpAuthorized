using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var keycloak = builder.AddKeycloak("keycloak", 8080)
     .WithImageTag("26.5.4")
     .WithRealmImport("./Realms")
     .WithOtlpExporter()
     .WithLifetime(ContainerLifetime.Persistent);

var mcp = builder.AddProject<McpAuthorized>("mcpauthorized")
                        .WithReference(keycloak)
                        .WaitFor(keycloak);

builder.AddMcpInspector("mcpinspector", new McpInspectorOptions() { InspectorVersion = "latest" })
    .WithEnvironment("ALLOWED_ORIGINS", "http://mcpinspector-mcpauth.dev.localhost:6274,http://localhost:6274")
    .WithMcpServer(mcp)
    .WaitFor(mcp);

builder.Build().Run();