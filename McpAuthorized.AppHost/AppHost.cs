using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var keycloak = builder.AddKeycloak("keycloak", 8080)
     .WithRealmImport("./Realms")
     .WithOtlpExporter()
     .WithLifetime(ContainerLifetime.Persistent);

builder.AddProject<McpAuthorized>("mcpauthorized")
                        .WithReference(keycloak)
                        .WaitFor(keycloak); ;

builder.Build().Run();