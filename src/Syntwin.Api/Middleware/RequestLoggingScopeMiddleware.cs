using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Syntwin.Api.Middleware;

public sealed class RequestLoggingScopeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingScopeMiddleware> _logger;

    public RequestLoggingScopeMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingScopeMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var scopeValues = new Dictionary<string, object?>
        {
            ["TraceId"] =
                Activity.Current?.TraceId.ToString() ??
                context.TraceIdentifier
        };

        AddRouteValue(context, scopeValues, "companyId", "CompanyId");
        AddRouteValue(context, scopeValues, "robotId", "RobotId");
        AddRouteValue(context, scopeValues, "factoryRunId", "FactoryRunId");
        AddRouteValue(context, scopeValues, "commandId", "CommandId");

        var action = context
            .GetEndpoint()?
            .Metadata
            .GetMetadata<ControllerActionDescriptor>();

        if (action?.ControllerName == "FactoryRuns")
        {
            AddRouteValue(context, scopeValues, "id", "FactoryRunId");
        }
        else if (action?.ControllerName == "Robots")
        {
            AddRouteValue(context, scopeValues, "id", "RobotId");
        }

        using (_logger.BeginScope(scopeValues))
        {
            await _next(context);
        }
    }

    private static void AddRouteValue(
        HttpContext context,
        IDictionary<string, object?> scopeValues,
        string routeKey,
        string scopeKey)
    {
        if (context.Request.RouteValues.TryGetValue(routeKey, out var value) &&
            value is not null)
        {
            scopeValues[scopeKey] = value;
        }
    }
}
