var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDotNetCounters()
    .WithLocalFileSink()
    // .WithProcessStartTrigger()
    .Register();

var app = builder.Build();


app.MapGet("/", (HttpContext httpContext) =>
{
    int random = (new Random()).Next(0, 3);
    // Running on 2 threads.
    Parallel.ForEach(Enumerable.Range(0, 2), (index, state) =>
    {
        CPUIntensive.CPUIntensiveService.BurnCPU(random, httpContext.RequestAborted);
    });
    return Results.Ok(random);
});

// Add an endpoint of `/dotnet-counters`
app.MapDotNetCounters("/dotnet-counters");

app.Run();
