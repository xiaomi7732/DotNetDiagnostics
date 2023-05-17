var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDotNetCounters()
    .WithLocalFileSink()
    /*Uncomment this line if you choose to output the result to the Azure Storage Blob.
        You will also need to setup the configurations for accessing the storage. Refer to 
        this wiki: https://github.com/xiaomi7732/DotNetDiagnostics/wiki/Using-Azure-Blob-for-Data-File-Output*/
    // .WithAzureBlobSink() 

    /*Uncomment this line if you want to enable dotnet-counters when the application starts.*/
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
