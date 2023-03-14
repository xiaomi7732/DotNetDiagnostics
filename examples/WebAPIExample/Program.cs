using DotNet.Diagnostics.Counters.WebHooks;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDotNetCounters();

var app = builder.Build();
app.MapGet("/", () => "Hello World!");
app.MapDotNetCounters("/dotnet-counters");
app.Run();