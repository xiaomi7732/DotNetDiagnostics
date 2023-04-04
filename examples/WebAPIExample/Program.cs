var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddSimpleConsole(opt =>
{
    opt.TimestampFormat = "[yyyy-MM-dd HH:mm:ss]";
    opt.SingleLine = true;
}));
builder.Services.AddDotNetCounters();
builder.Services.AddDotNetCounterLocalFileSink();
builder.Services.AddDotNetCounterAzureBlobSink();

// Application insights and its sink
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddDotNetCounterApplicationInsightsSink();

var app = builder.Build();
app.MapGet("/", () => "Hello World!");
app.MapDotNetCounters("/dotnet-counters");
app.Run();