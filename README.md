# Hangfire.Correlate

[Hangfire](https://www.hangfire.io/) integration of [Correlate](https://github.com/skwasjer/Correlate) to add correlation id support to Hangfire background/scheduled jobs.

## Installation

Install Hangfire.Correlate via the Nuget package manager or `dotnet` cli.

```powershell
dotnet add package Hangfire.Correlate
```

---

[![Main workflow](https://github.com/skwasjer/Hangfire.Correlate/actions/workflows/main.yml/badge.svg)](https://github.com/skwasjer/Hangfire.Correlate/actions/workflows/main.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=skwasjer_Hangfire.Correlate&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=skwasjer_Hangfire.Correlate)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=skwasjer_Hangfire.Correlate&metric=coverage)](https://sonarcloud.io/component_measures?id=skwasjer_Hangfire.Correlate&metric=coverage)


| | | |
|---|---|---|
| `Hangfire.Correlate` | [![NuGet](https://img.shields.io/nuget/v/Hangfire.Correlate.svg)](https://www.nuget.org/packages/Hangfire.Correlate/) [![NuGet](https://img.shields.io/nuget/dt/Hangfire.Correlate.svg)](https://www.nuget.org/packages/Hangfire.Correlate/) | Correlate integration with Hangfire. |

## Correlation ID flow

The Correlate framework provides an ambient correlation context scope, that makes it easy to track a Correlation ID passing through (micro)services.

This library specifically provides a job filter for [Hangfire](https://www.hangfire.io/) and ensures each job is performed (run) in its own correlation context provided by [Correlate](https://github.com/skwasjer/Correlate).

### Job creation

Whenever a job is enqueued in an existing correlation context, the current correlation id will be attached to the job as a job parameter. If no correlation context is available when the job is enqueued, no parameter is added.

### Job execution

When the job is 'performed' (in Hangfire terms), the job parameter (for correlation id) that was added during job creation will be used to create a new correlation context, thus reviving the correlation context. This means that even in distributed scenarios, the same correlation id is used to process the job.
> If no correlation id was stored with the job, yet also to remain backwards compatible with existing jobs (prior to Correlate integration), the job id will be used instead. 

### Job continuation

A continuation job will inherit the correlation id from the parent job, unless explicitly inside an active correlation context.

## Usage ###

Configure Hangfire to use Correlate.

### Using a `IServiceProvider`

Use the configuration extension with `IServiceProvider` to configure Hangfire with Correlate.

Add package dependencies:
- [Correlate.DependencyInjection](https://github.com/skwasjer/Correlate)

```csharp
services
    .AddLogging(logging => logging.AddConsole())
    .AddCorrelate()
    .AddHangfire((serviceProvider, config) => config
        .UseCorrelate(serviceProvider)
        .(...)
    );
```

### Using built-in configuration extensions ###

Use the Hangfire built-in configuration extensions to enable Correlate.

> This is no longer the recommended approach and will most likely be removed in a future version.

```csharp
ILoggerFactory loggerFactory = new LoggerFactory();
loggerFactory.AddConsole();

GlobalConfiguration.Configuration
    .UseCorrelate(loggerFactory)
    .(...);
```

## Enqueue jobs

This example illustrates how jobs that are enqueued, inherit the Correlation ID from the ambient correlation context if inside one.

```csharp
public class MyService
{
    private IAsyncCorrelationManager _correlationManager;
    private IBackgroundJobClient _client;

    public MyService(IAsyncCorrelationManager correlationManager, IBackgroundJobClient client)
    {
        _correlationManager = _correlationManager;
        _client = client;
    }

    public async Task DoWork()
    {
        // Without ambient correlation context, the job id will be used.
        _client.Enqueue(() => Thread.Sleep(1000));

        // Perform work in new correlation context.
        string parentJobId = await _correlationManager.CorrelateAsync(async () =>
        {
            // This job be executed with the Correlation ID from
            // the ambient correlation context which is automatically
            // generated.
            _client.Enqueue(() => Thread.Sleep(1000));

            // Do stuff.
            await ..

            // This job will be also be executed with the same Correlation ID.
            return _client.Enqueue<MyJob>(myJob => myJob.CallApiAsync());
        });

          // This job will be also be executed with the same Correlation ID
          // as which MyJob.CallApiAsync() was executed, even though it is
          // outside the ambient correlation context, because it is a 
          // continuation and we used its job id to enqueue it.
        _client.ContinueJobWith(parentJobId, () => Thread.Sleep(1000));
    }
}
```

> Note: when using Correlate integration for ASP.NET Core, each request is already scoped to a correlation context, and so there is no need to wrap the enqueueing of jobs with `IAsyncCorrelationManager`/`ICorrelationManager`.

## More info

See [Correlate](https://github.com/skwasjer/Correlate) documentation for further integration with ASP.NET Core, `IHttpClientFactory` and for other extensions/libraries.

### Supported .NET targets
- .NET Standard 2.0

### Build requirements
- Visual Studio 2022
- .NET 6.0
- .NET Core 3.1

#### Contributions
PR's are welcome. Please rebase before submitting, provide test coverage, and ensure the AppVeyor build passes. I will not consider PR's otherwise.

#### Contributors
- skwas (author/maintainer)
