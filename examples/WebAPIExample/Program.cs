var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddSimpleConsole(opt =>
{
    opt.TimestampFormat = "[yyyy-MM-dd HH:mm:ss]";
    opt.SingleLine = true;
}));

builder.Services.AddDotNetCounters(pipeline =>
{
    // Configure the pipeline to adding triggers, a job dispatcher and various sinks.
    pipeline
        .AddProcessStartTrigger()
        /* Enables job dispatcher that uses an Azure Storage to coordinate.
        Refer to https://github.com/xiaomi7732/DotNetDiagnostics/wiki/How-to-run-dotnet-counters-in-multiple-instances
        for more details. */
        // .AddAzureBlobJobDispatcher()

        // Local file sink
        .AddLocalFileSink()

        /* Enables the sink to output data to an azure blob storage. */
        // .AddAzureBlobSink()

        // Application insights sink
        .AddApplicationInsightsSink();
});

// Register services for Application Insights, this is required for the app insights sink to work.
builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();
app.MapGet("/", () => "Hello World!");
app.MapDotNetCounters("/dotnet-counters");
app.Run();