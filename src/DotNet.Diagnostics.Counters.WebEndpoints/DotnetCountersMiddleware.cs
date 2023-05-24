using System.Net;
using System.Text.Json;
using DotNet.Diagnostics.Core;
using DotNet.Diagnostics.Core.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNet.Diagnostics.Counters.WebEndpoints;

public class DotNetCounterMiddleware
{
    private readonly RequestDelegate _next;
    private readonly DotNetCountersWebhookOptions _options;
    private readonly IDotNetCountersClient _dotnetCountersClient;
    private readonly IEnumerable<IJobDispatcher<DotNetCountersJobDetail>> _jobDispatchers;
    private readonly EnvVarMatcher _jobFilter;
    private readonly ICounterPayloadSet _counterPayloadSet;
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _jsonSerializationOptions;

    public DotNetCounterMiddleware(RequestDelegate next,
        IOptions<DotNetCountersWebhookOptions> dotnetCountersWebhookOptions,
        IDotNetCountersClient dotnetCountersClient,
        IEnumerable<IJobDispatcher<DotNetCountersJobDetail>> jobDispatchers,
        EnvVarMatcher jobFilter,
        ICounterPayloadSet counterPayloadSet,
        ILogger<DotNetCounterMiddleware> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _next = next;
        _options = dotnetCountersWebhookOptions?.Value ?? throw new ArgumentNullException(nameof(dotnetCountersWebhookOptions));
        _dotnetCountersClient = dotnetCountersClient ?? throw new ArgumentNullException(nameof(dotnetCountersClient));
        _jobFilter = jobFilter ?? throw new ArgumentNullException(nameof(jobFilter));
        _counterPayloadSet = counterPayloadSet ?? throw new ArgumentNullException(nameof(counterPayloadSet));
        _jsonSerializationOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        _jobDispatchers = jobDispatchers ?? Enumerable.Empty<IJobDispatcher<DotNetCountersJobDetail>>();

        int jobDispatchCount = _jobDispatchers.Count();
        if (jobDispatchCount == 0)
        {
            _logger.LogInformation("No job dispatcher configured. Fits best for single instance environment.");
        }
        else if (jobDispatchCount == 1)
        {
            _logger.LogInformation("1 job dispatcher configured. Support multiple-instance environment.");
        }
        else
        {
            _logger.LogWarning("More than 1 Job dispatcher configured. Are you doing it on purpose? Job dispatcher count: {count}", jobDispatchCount);
        }
    }

    public async Task InvokeAsync(HttpContext httpContext,
        ILogger<DotNetCounterMiddleware>? logger)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (!IsMethod(httpContext.Request, HttpMethod.Put) &&
            !IsMethod(httpContext.Request, HttpMethod.Get))
        {
            throw new InvalidOperationException($"Unsupported HttpMethod: {httpContext.Request.Method}");
        }

        CancellationToken cancellationToken = httpContext.RequestAborted;
        RequestBodyContract? body = await JsonSerializer.DeserializeAsync<RequestBodyContract>(httpContext.Request.Body, _jsonSerializationOptions, cancellationToken).ConfigureAwait(false);
        if (body is null)
        {
            throw new InvalidOperationException("Request body is invalid.");
        }

        if (!string.IsNullOrEmpty(_options.InvokingSecret) && !string.Equals(body.InvokingSecret, _options.InvokingSecret))
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            logger?.LogError("Invalid invoking secret: {key}", body.InvokingSecret);

            await httpContext.Response.WriteAsJsonAsync(new RequestError() { StatusCode = (int)HttpStatusCode.Forbidden, Message = "Unauthorized access. Invalid invoking secret." }, cancellationToken);
            return;
        }

        if (IsMethod(httpContext.Request, HttpMethod.Get))
        {
            await HandleGetRequestAsync(httpContext, body, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (IsMethod(httpContext.Request, HttpMethod.Put))
        {
            await HandlePutRequestAsync(httpContext, body, cancellationToken).ConfigureAwait(false);
            return;
        }

        // Skip calling the next delegate/middleware in the pipeline, because this is a terminal.
        // await _next(context);
    }

    private async Task HandleGetRequestAsync(HttpContext httpContext, RequestBodyContract _, CancellationToken cancellationToken)
    {
        // TODO: Get serializer configuration
        // TODO: Error handling

        if (_counterPayloadSet.Data.Count == 0)
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.NoContent;
            return;
        }

        try
        {
            await httpContext.Response.WriteAsJsonAsync<ICounterPayloadSet>(_counterPayloadSet, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await httpContext.Response.WriteAsJsonAsync(ex.Message, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task HandlePutRequestAsync(HttpContext httpContext, RequestBodyContract body, CancellationToken cancellationToken)
    {
        if (body.EnvVarFilters is not null && !_jobFilter.MatchAll(body.EnvVarFilters))
        {
            // Dispatch the job since it doesn't belong to this instance;
            if (_jobDispatchers.Count() == 0)
            {
                _logger.LogWarning("Got job doesn't belong to the current instance but no active job dispatcher. Have you forgot to register the job dispatchers?");

                // TODO: Return better error
                httpContext.Response.StatusCode = (int)HttpStatusCode.PreconditionFailed;
                await httpContext.Response.WriteAsJsonAsync(new
                {
                    Message = "No valid dispatcher to handle jobs that doesn't belong to the current instance. Configure the dotnet-counter pipeline with proper job dispatcher first.",
                    StatusCode = httpContext.Response.StatusCode,
                }, cancellationToken).ConfigureAwait(false);
                return;
            }
            await DispatchJobsAsync(body, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Otherwise, execute it immediately
            await ExecuteJobAsync(httpContext, body, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task DispatchJobsAsync(RequestBodyContract body, CancellationToken cancellationToken)
    {
        // TODO: Build an extension method for the conversion.
        DotNetCountersJobDetail jobDetail = new DotNetCountersJobDetail()
        {
            Filter = body.EnvVarFilters,
            IsEnabled = body.IsEnabled,
        };

        foreach (IJobDispatcher<DotNetCountersJobDetail> jobDispatcher in _jobDispatchers)
        {
            await jobDispatcher.DispatchAsync(jobDetail, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ExecuteJobAsync(HttpContext httpContext, RequestBodyContract body, CancellationToken cancellationToken)
    {
        await ExecuteJobAsync(body.IsEnabled, cancellationToken).ConfigureAwait(false);
        await httpContext.Response.WriteAsJsonAsync(new ResponseBodyContract() { IsEnabled = body.IsEnabled }).ConfigureAwait(false);
    }

    private Task ExecuteJobAsync(bool isEnabled, CancellationToken cancellationToken)
    {
        if (isEnabled)
        {
            return _dotnetCountersClient.EnableAsync(cancellationToken: cancellationToken);
        }
        else
        {
            return _dotnetCountersClient.DisableAsync(cancellationToken: cancellationToken);
        }
    }

    private bool IsMethod(HttpRequest request, HttpMethod compareTo)
        => string.Equals(request.Method, compareTo.Method, StringComparison.Ordinal);
}