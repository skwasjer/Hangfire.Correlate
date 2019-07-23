using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hangfire.Correlate.Extensions
{
	public static class LoggingExtensions
	{
#if NETCOREAPP1_1 || NETFRAMEWORK
		public static IServiceCollection ForceEnableLogging(this IServiceCollection services)
		{
			return services
				.AddSingleton(s => new LoggerFactory().ForceEnableLogging())
				.AddLogging();
		}
#else
		public static IServiceCollection ForceEnableLogging(this IServiceCollection services)
		{
			return services.AddLogging(logging => logging.AddProvider(new TestLoggerProvider()));
		}
#endif

		public static ILoggerFactory ForceEnableLogging(this ILoggerFactory loggerFactory)
		{
			loggerFactory.AddProvider(new TestLoggerProvider());
			return loggerFactory;
		}

		private class TestLoggerProvider : ILoggerProvider
		{
			private TestLogger _testLogger;

			public void Dispose()
			{
			}

			public ILogger CreateLogger(string categoryName)
			{
				return _testLogger ?? (_testLogger = new TestLogger());
			}
		}
	}
}