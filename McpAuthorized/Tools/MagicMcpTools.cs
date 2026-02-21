using ModelContextProtocol.Server;
using System.ComponentModel;

/// <summary>
/// Sample MCP tools for demonstration purposes.
/// These tools can be invoked by MCP clients to perform various operations.
/// </summary>
internal class MagicMcpTools(IHttpContextAccessor httpContext, ILogger<MagicMcpTools> logger)
{
    private readonly IHttpContextAccessor _httpContext = httpContext;
    private readonly ILogger<MagicMcpTools> _logger = logger;

    [McpServerTool]
    [Description("Generates a random number between the specified minimum and maximum values.")]
    public int GetRandomNumber(
        [Description("Minimum value (inclusive)")] int min = 0,
        [Description("Maximum value (exclusive)")] int max = 100)
    {
        _logger.LogInformation("Generating a random number between {Min} and {Max}", min, max);

        return Random.Shared.Next(min, max);
    }

    [McpServerTool(UseStructuredContent = true)]
    [Description("Returns the authenticated application from the JWT")]
    public Dictionary<string, string> WhoAmI()
    {
        _logger.LogInformation("Current user {@User}", _httpContext.HttpContext.User);

        return _httpContext.HttpContext.User.Claims.ToDictionary(x => x.Type, x => x.Value);
    }
}
