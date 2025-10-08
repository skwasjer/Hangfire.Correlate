using Correlate.DependencyInjection;
using Hangfire.Logging;
using Hangfire.Server;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Hangfire.Correlate;

/// <summary>
/// Tests Hangfire integration with the <see cref="GlobalConfigurationExtensions.UseCorrelate(IGlobalConfiguration,IServiceProvider)" />
/// overload.
/// </summary>
/// <remarks>
/// Parallel test execution is not supported since we use memory storage with Hangfire that is being set into a static property
/// Storage.Current. When tests are run in parallel, the test that last set the storage will win, while the others will break. This is also
/// true for other Hangfire dependencies, but they do not directly affect our tests atm.
/// </remarks>
public class HangfireServiceProviderTests : HangfireIntegrationTests
{
    private readonly IServiceProvider _services;

    public HangfireServiceProviderTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
        // Register Correlate, Hangfire and tell Hangfire to use Correlate.
        IServiceCollection serviceCollection = new ServiceCollection();
        serviceCollection
            .AddCorrelate()
            .AddHangfire(
                (s, config) =>
                {
                    config
                        .UseCorrelate(s)
                        .UseLogProvider(Substitute.For<ILogProvider>())
                        .UseInMemoryStorage();
                }
            );

        // Below, dependencies for test only.

        // Register a typed client which is used by the job to call an endpoint.
        // We use it to assert the request header contains the correlation id.
        serviceCollection
            .AddHttpClient<TestService>(
                client =>
                {
                    client.BaseAddress = new Uri("http://0.0.0.0");
                }
            )
            .ConfigurePrimaryHttpMessageHandler(() => MockHttp)
            .CorrelateRequests();

        serviceCollection
            .AddSingleton(ExecutedJobs)
            .AddSingleton(testOutputHelper)
            // In ASP.NET Core, you'd use UseHangfireServer() which internally creates BackgroundJobServer.
            // Since we're testing all target frameworks, just register manually.
            .AddTransient<IBackgroundProcessingServer, BackgroundJobServer>();

        _services = serviceCollection.BuildServiceProvider();
    }

    public override Task DisposeAsync()
    {
        (_services as IDisposable)?.Dispose();
        return base.DisposeAsync();
    }

    protected override IBackgroundProcessingServer CreateServer()
    {
        return _services.GetRequiredService<IBackgroundProcessingServer>();
    }

    protected override IBackgroundJobClient CreateClient()
    {
        return _services.GetRequiredService<IBackgroundJobClient>();
    }
}
