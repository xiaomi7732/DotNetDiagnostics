var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddSimpleConsole(opt =>
{
    opt.TimestampFormat = "[yyyy-MM-dd HH:mm:ss]";
    opt.SingleLine = true;
}));
builder.Services.AddDotNetCounters();
builder.Services.AddDotNetCounterLocalFileSink();
builder.Services.AddDotNetCounterAzureBlobSink();

var app = builder.Build();
app.MapGet("/", () => "Hello World!");
app.MapDotNetCounters("/dotnet-counters");
app.Run();