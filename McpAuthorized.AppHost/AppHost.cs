using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var keycloak = builder.AddKeycloak("keycloak", 8080)
     .WithRealmImport("./Realms")
     .WithOtlpExporter()
     .WithLifetime(ContainerLifetime.Persistent);

var mcp = builder.AddProject<McpAuthorized>("mcpauthorized")
                        .WithReference(keycloak)
                        .WaitFor(keycloak)
                        .WithHttpEndpoint(port: 6102, name: "http")
                        .WithHttpsEndpoint(port: 5092, name: "https")
                        .WithUrlForEndpoint("http", u => u.Url = "http://mcpauthorized.dev.localhost:6102")
                        .WithUrlForEndpoint("https", u => u.Url = "https://mcpauthorized.dev.localhost:5092");

builder.AddMcpInspector("mcpinspector")
    .WithReference(mcp)
    .WaitFor(mcp);

builder.Build().Run();