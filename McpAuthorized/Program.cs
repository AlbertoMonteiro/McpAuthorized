using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.AspNetCore.Authentication;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

var serverUrl = "account";
UriBuilder uriBuilder = new(builder.Configuration.GetValue<string>("KEYCLOAK_HTTP"));
uriBuilder.Host = $"keycloak-mcpauth.dev.{uriBuilder.Host}";
uriBuilder.Path = "realms/api";
var inMemoryOAuthServerUrl = uriBuilder.Uri.ToString();

builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        if (builder.Environment.IsDevelopment())
        {
            options.RequireHttpsMetadata = false;
        }

        // Configure to validate tokens from our in-memory OAuth server
        options.Authority = inMemoryOAuthServerUrl;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidAudience = serverUrl, // Validate that the audience matches the resource metadata as suggested in RFC 8707
            ValidIssuer = inMemoryOAuthServerUrl,
            NameClaimType = "name",
            RoleClaimType = "roles"
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var name = context.Principal?.Identity?.Name ?? "unknown";
                var email = context.Principal?.FindFirstValue("preferred_username") ?? "unknown";
                Console.WriteLine($"Token validated for: {name} ({email})");
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"Authentication failed: {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                Console.WriteLine($"Challenging client to authenticate with Entra ID");
                return Task.CompletedTask;
            }
        };
    })
    .AddMcp(options =>
    {
        _ = 1;
        options.ResourceMetadata = new()
        {
            Resource = serverUrl,
            ResourceDocumentation = "https://docs.example.com/api/weather",
            AuthorizationServers = { inMemoryOAuthServerUrl },
            ScopesSupported = ["mcp:tools"],
        };
    });

builder.Services.AddAuthorization();

// Add the MCP services: the transport to use (http) and the tools to register.
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<MagicMcpTools>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapMcp()
    .RequireAuthorization();

app.Run();
