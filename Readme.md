# Welcome to Open DotNET Diagnostics

Our vision:

Integrate .NET diagnostics tools into your code and seamlessly deliver results to multiple destinations.

![Vision for the project](./src/../images/IssuesToSolve.png)

We aim to alleviate the following **pain points**:

1. You no longer need to deliver dotnet diagnostic tool binaries such as dotnet-counters, dotnet-trace, or dotnet-gcdump to your environment..
   1. Some environments, such as containers, make it inconvenient to add additional binaries, while others, like Azure App Service/WebSite, are sandboxed and unable to run .NET tools.
   2. With our solution, you can enjoy a consistent experience whether you are diagnosing issues locally or remotely.

2. You no longer need to export diagnostic data, such as `dotnet-counter` output, from a constrained environment.
    1. By adding proper sinks, you can easily access these files through Kudu or Azure Blob Storage and so on.
    2. Your data will persist externally even if your machine or containers are recycled.

3. With our solution, you can write once and run everywhere, including locally, on Azure WebSite, in containers, or on AKS, with a unified experience.

_Please note that while this repository is open source, it is not a Microsoft/dotnet repository. We welcome contributions from anyone interested in improving our solution._

Our approach places a stronger emphasis on the developer experience, and thus requires a reasonable amount of code instrumentation. If you prefer an operational approach that requires no code changes, we recommend checking out the official [dotnet-monitor](https://github.com/dotnet/dotnet-monitor) repository.

## Get Started (dotnet-counters)

Assuming you have an ASP.NET Core WebAPI project:

1. Add NuGet packages:
    * **DotNet.Diagnostics.Counters.WebHooks** - to expose an endpoint for enabling/disabling `dotnet-counters`.
    * **DotNet.Diagnostics.Counters.Sinks.LocalFile** - to export the data to a local file (and in app service, to application logs folder).

2. Instrument the code to register the proper service and map the end point, for example:

    ```csharp
    var builder = WebApplication.CreateBuilder(args);

    // Add services needed to run dotnet-counters
    builder.Services.AddDotNetCounters();
    // Add services needed for the local file sink for dotnet-counters
    builder.Services.AddDotNetCounterLocalFileSink();

    var app = builder.Build();

    app.MapGet("/", () => "Hello World!");
    
    // Add an endpoint of `/dotnet-counters`
    app.MapDotNetCounters("/dotnet-counters");
    app.Run();
    ```

3. Optionally, customize the settings, for example, you could specify a invoking secret than the default of `1123` by putting this in your [appsettings.json](./examples/WebAPIExample/appsettings.Development.json):

    ```json
    "DotNetCountersWebhook": {
        "InvokingSecret": "1111"
    },
    ```

4. Run your app.

5. To enable `dotnet-counters`, invoke a `HttpPUT` on the endpoint, for example:

    ![Invoking dotnet-counters](./images/InvokingDotNetCounters.png)

    _Tips: You can turn off `dotnet-counters` at anytime by invoke another PUT request with `isEnabled` parameter set to false._

1. Get the output
    * In a local environment, by default, the file is in `%tmp%`, you will have files like `Counters_2023031600.csv`;
    * In `Azure App Service`, the default output path would be `%HOME%/LogFiles/Application/`, and the file name would carry a unique id for the service instance, like this:
        * Counters_82177b41d89d4b2dce789b4903a7e0dc0a76412697ac6069b750097059c09ed7_2023031523.csv
        ![Counters Output on Kudu](./images/CountersOutputOnKudu.png)

1. And you shall be able to download analysis the result in tools you already familiar with, for example, in the Excel:

    ![Analysis example in excel for working set](./images/DotNetCounterWorkingSetExample.png)

    What we see: it is a pretty small amount of `working set` used over the period, yet we could still see dips, probably GC?

## Road map

1. Add support for more .NET diagnostics tools.
1. Update to support more complex environments - scaled out multiple instances.
1. Support triggers - that automatically starts the diagnostic tools.
1. Add guidance for extending sinks.