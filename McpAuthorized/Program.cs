using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpLogging;
using ModelContextProtocol.AspNetCore.Authentication;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();

builder.AddServiceDefaults();

builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestPropertiesAndHeaders | HttpLoggingFields.ResponsePropertiesAndHeaders | HttpLoggingFields.Duration;
    options.CombineLogs = true;
    options.RequestHeaders.Add("Authorization");
    options.ResponseHeaders.Add("WWW-Authenticate");
});

builder.Services.AddProblemDetails();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options => options.TokenValidationParameters.NameClaimType = ClaimTypes.GivenName)
    .AddMcp(options =>
    {
        _ = 1;
        options.ResourceMetadata = new()
        {
            AuthorizationServers = { builder.Configuration["Authentication:Schemes:Bearer:Authority"]! },
            ScopesSupported = ["mcp:tools", "profile", "email", "roles"],
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(x => x.AddDefaultPolicy(c => c.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Mcp-Session-Id", "Mcp-Protocol-Version")));

// Add the MCP services: the transport to use (http) and the tools to register.
builder.Services
    .AddMcpServer()
    .AddAuthorizationFilters()
    .WithHttpTransport()
    .WithTools<MagicMcpTools>();

var app = builder.Build();

app.UseHttpLogging();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapMcp("/mcp")
    .RequireAuthorization();

await app.RunAsync();