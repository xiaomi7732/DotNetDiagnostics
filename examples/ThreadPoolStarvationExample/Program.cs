using ThreadPoolStarvationExample;

var builder = WebApplication.CreateBuilder(args);

// Enable dotnet-counters to capture the thread pool starvation
builder.Services.AddDotNetCounters(pipeline =>
{
    pipeline.AddLocalFileSink();
    pipeline.AddProcessStartTrigger();
});
// ~

builder.Services.AddSingleton<ThreadEater>();

var app = builder.Build();

/// <summary>
/// Invoking this repeatedly to see the thread pool starvation.
/// For example: bombardier-windows-amd64.exe http://localhost:5212/starv
/// </summary>
app.MapGet("/starv", (ThreadEater te) =>
{
    te.StarveAllTheWay();
    return "Damage done!";
});

/// <summary>
/// Use this for comparison when there's no thread pool starvation
/// </summary>
/// <returns></returns>
app.MapGet("/nostarv", async () =>
{
    await Task.Delay(TimeSpan.FromMilliseconds(500));
    return "No damage!";
});

app.MapDotNetCounters("/dotnet-counters");

app.Run();
