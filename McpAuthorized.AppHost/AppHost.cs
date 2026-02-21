using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var keycloak = builder.AddKeycloak("keycloak", 8080)
     .WithRealmImport("./Realms")
     .WithOtlpExporter()
     .WithLifetime(ContainerLifetime.Persistent);

var mcp = builder.AddProject<McpAuthorized>("mcpauthorized")
                        .WithReference(keycloak)
                        .WaitFor(keycloak);

builder.AddMcpInspector("mcpinspector")
    .WithReference(mcp)
    .WaitFor(mcp);

builder.Build().Run();