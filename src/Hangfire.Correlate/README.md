[Hangfire](https://www.hangfire.io/) integration of [Correlate](https://github.com/skwasjer/Correlate) to add correlation id support to Hangfire background/scheduled jobs.

## Registration example ###

Configure Hangfire to use Correlate.

### Using built-in configuration extensions ###

Use the Hangfire built-in configuration extensions to enable Correlate.

```csharp
ILoggerFactory loggerFactory = new LoggerFactory();
loggerFactory.AddConsole();

GlobalConfiguration.Configuration
    .UseCorrelate(loggerFactory)
    .(...);
```

### Using a `IServiceProvider`

Alternatively (but recommended), use `IServiceProvider` to configure Hangfire with Correlate.

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
