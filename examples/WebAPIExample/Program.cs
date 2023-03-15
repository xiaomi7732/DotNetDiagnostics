var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddSimpleConsole(opt =>
{
    opt.SingleLine = true;
}));
builder.Services.AddDotNetCounters();
builder.Services.AddDotNetCounterLocalFileSink();

var app = builder.Build();
app.MapGet("/", () => "Hello World!");
app.MapDotNetCounters("/dotnet-counters");
app.Run();