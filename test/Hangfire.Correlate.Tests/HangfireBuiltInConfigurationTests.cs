using Correlate;
using Correlate.Http;
using Hangfire.MemoryStorage;
using Hangfire.Server;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace Hangfire.Correlate;

/// <summary>
/// Tests Hangfire integration with the <see cref="GlobalConfigurationExtensions.UseCorrelate(IGlobalConfiguration,ILoggerFactory)" /> overload.
/// </summary>
/// <remarks>
/// Parallel test execution is not supported since we use memory storage with Hangfire that is being set into a static property Storage.Current. When tests are run in parallel, the test that last set the storage will win, while the others will break. This is also true for other Hangfire dependencies, but they do not directly affect our tests atm.
/// </remarks>
public class HangfireBuiltInConfigurationTests : HangfireIntegrationTests
{
    private readonly LoggerFactory _loggerFactory;

    public HangfireBuiltInConfigurationTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
        _loggerFactory = new LoggerFactory();

        GlobalConfiguration.Configuration
            .UseCorrelate(_loggerFactory)
            .UseActivator(
                new BackgroundTestExecutorJobActivator(() =>
                    new BackgroundTestExecutor(
                        CreateTestService(),
                        new CorrelationContextAccessor(),
                        ExecutedJobs,
                        testOutputHelper
                    )
                )
            )
            .UseMemoryStorage();
    }

    private TestService CreateTestService()
    {
        var correlatingHttpMessageHandler = new CorrelatingHttpMessageHandler(
            new CorrelationContextAccessor(),
            Options.Create(new CorrelateClientOptions())
        ) { InnerHandler = MockHttp };
        return new TestService(
            new HttpClient(correlatingHttpMessageHandler, true) { BaseAddress = new Uri("http://0.0.0.0") }
        );
    }

    public override Task DisposeAsync()
    {
        _loggerFactory.Dispose();
        return base.DisposeAsync();
    }

    protected override IBackgroundProcessingServer CreateServer()
    {
        return new BackgroundJobServer();
    }

    protected override IBackgroundJobClient CreateClient()
    {
        return new BackgroundJobClient();
    }
}
