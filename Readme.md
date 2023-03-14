# Welcome to Open DotNET Diagnostics

Deliver the dotnet diagnostics tools to your production environments, and pull the result to various destinations.

Our vision:

![Vision for the project](./src/../images/IssuesToSolve.png)

1. You don't need to deliver dotnet diagnostic tools (like dotnet-counters, dotnet-trace, dotnet-gcdump) to the environment.
   1. There are environment like containers that it is not convenient to get additional binaries;
   1. There are hosted environment like Azure App Service/WebSite, which is sandboxed, that you can't run .NET tools.
   1. It's going to be consistent experience for local or remote diagnosing.

1. You don't need to extract the diagnostic data, dotnet-counter output for example, out of the constraint out of the environment.
    1. By using proper sinks, you could easily access those files.
    1. Containers might be recycled, data will be persistent externally.

## Get Started

// TODO: Add getting started.
