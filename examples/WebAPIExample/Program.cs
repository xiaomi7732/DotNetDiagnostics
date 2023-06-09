var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddSimpleConsole(opt =>
{
    opt.TimestampFormat = "[yyyy-MM-dd HH:mm:ss]";
    opt.SingleLine = true;
}));

builder.Services.AddDotNetCounters(pipeline =>
{
    pipeline
        .WithProcessStartTrigger()
        .WithAzureBlobJobDispatcher()
        .WithLocalFileSink()
        .WithAzureBlobSink()
        .WithApplicationInsightsSink();
});

// Register services for Application Insights, this is required for the app insights sink to work.
builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();
app.MapGet("/", () => "Hello World!");
app.MapDotNetCounters("/dotnet-counters");
app.Run();