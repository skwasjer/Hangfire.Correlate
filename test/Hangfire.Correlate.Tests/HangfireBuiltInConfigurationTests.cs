using System;
using Hangfire.MemoryStorage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Hangfire.Correlate
{
	/// <summary>
	/// Tests Hangfire integration with the <see cref="IGlobalConfigurationExtensions.UseCorrelate(IGlobalConfiguration,ILoggerFactory)" /> overload.
	/// </summary>
	/// <remarks>
	///	Parallel test execution is not supported since we use memory storage with Hangfire that is being set into a static property Storage.Current. When tests are run in parallel, the test that last set the storage will win, while the others will break. This is also true for other Hangfire dependencies, but they do not directly affect our tests atm.
	/// </remarks>
	[Collection(nameof(HangfireIntegrationTests))]
	public class HangfireBuiltInConfigurationTests : HangfireIntegrationTests
	{
		public HangfireBuiltInConfigurationTests(ITestOutputHelper toh) : base(toh, services =>
			services
				.AddHangfire((s, config) =>
				{
					toh.WriteLine(nameof(HangfireServiceProviderTests) + DateTime.Now.Ticks);
					config
						.UseCorrelate(s.GetRequiredService<ILoggerFactory>())
						.UseMemoryStorage();
				})
		)
		{
		}
	}
}