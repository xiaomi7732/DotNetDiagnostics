using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNet.Diagnostics.Counters.WebHooks;

public class DotNetCounterMiddleware
{
    private readonly RequestDelegate _next;
    private readonly DotNetCountersWebhookOptions _healthCheckOptions;
    private readonly IDotNetCountersClient _dotnetCountersClient;
    private readonly JsonSerializerOptions _jsonSerializationOptions;

    public DotNetCounterMiddleware(RequestDelegate next,
        IOptions<DotNetCountersWebhookOptions> healthCheckOptions,
        IDotNetCountersClient dotnetCountersClient)
    {
        _next = next;
        _healthCheckOptions = healthCheckOptions?.Value ?? throw new ArgumentNullException(nameof(healthCheckOptions));
        _dotnetCountersClient = dotnetCountersClient ?? throw new ArgumentNullException(nameof(dotnetCountersClient));
        _jsonSerializationOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

    public async Task InvokeAsync(HttpContext httpContext,
        ILogger<DotNetCounterMiddleware>? logger)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (!string.Equals(httpContext.Request.Method, HttpMethod.Put.Method, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported HttpMethod: {httpContext.Request.Method}");
        }

        CancellationToken cancellationToken = httpContext.RequestAborted;

        RequestBodyContract? body = await JsonSerializer.DeserializeAsync<RequestBodyContract>(httpContext.Request.Body, _jsonSerializationOptions, cancellationToken).ConfigureAwait(false);

        if (body is null)
        {
            throw new InvalidOperationException("Request body is invalid.");
        }

        if (body.IsEnabled)
        {
            await _dotnetCountersClient.EnableAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _dotnetCountersClient.DisableAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        await httpContext.Response.WriteAsJsonAsync(new ResponseBodyContract() { IsEnabled = body.IsEnabled }).ConfigureAwait(false);

        // Skip calling the next delegate/middleware in the pipeline, because this is a terminal.
        // await _next(context);
    }
}