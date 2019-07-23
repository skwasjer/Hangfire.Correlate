using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Correlate;
using Correlate.DependencyInjection;
using Correlate.Http;
using FluentAssertions;
using Hangfire.Correlate.Extensions;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using Microsoft.Extensions.DependencyInjection;
using MockHttp;
using Xunit;
using Xunit.Abstractions;
using CorrelationManager = Correlate.CorrelationManager;

namespace Hangfire.Correlate
{
	public abstract class HangfireIntegrationTests : IDisposable
	{
		private readonly ITestOutputHelper _testOutputHelper;
		private readonly ServiceProvider _services;
		private readonly JobStorage _jobStorage;
		private readonly IBackgroundJobClient _client;
		private readonly CorrelationManager _correlationManager;
		private readonly ICollection<object> _executedJobs;
		private readonly MockHttpHandler _mockHttp;

		protected HangfireIntegrationTests(ITestOutputHelper testOutputHelper, Action<IServiceCollection> configureServices)
		{
			_testOutputHelper = testOutputHelper;
			_executedJobs = new List<object>();

			_mockHttp = new MockHttpHandler();
			_mockHttp.Fallback.Respond(HttpStatusCode.OK);

			// Register Correlate. The provided action should register Hangfire and tell Hangfire to use Correlate.
			IServiceCollection serviceCollection = new ServiceCollection()
				.AddCorrelate();
			configureServices(serviceCollection);

			// Below, dependencies for test only.

			// Register a typed client which is used by the job to call an endpoint.
			// We use it to assert the request header contains the correlation id.
			serviceCollection
				.AddHttpClient<TestService>(client =>
				{
					client.BaseAddress = new Uri("http://0.0.0.0");
				})
				.ConfigurePrimaryHttpMessageHandler(() => _mockHttp)
				.CorrelateRequests();

			serviceCollection
				.AddSingleton<BackgroundJobServer>()
				.AddSingleton(_executedJobs)
				.AddSingleton(testOutputHelper)
				.ForceEnableLogging();

			_services = serviceCollection.BuildServiceProvider();

			_jobStorage = _services.GetRequiredService<JobStorage>();
			_client = _services.GetRequiredService<IBackgroundJobClient>();
			_correlationManager = _services.GetRequiredService<CorrelationManager>();
		}

		public void Dispose()
		{
			_services.Dispose();
		}

		[Fact]
		public async Task Given_job_is_queued_outside_correlationContext_should_use_jobId_as_correlationId()
		{
			string jobId = _client.Enqueue<BackgroundTestExecutor>(job => job.RunAsync(250, null));
			var expectedJob = new BackgroundTestExecutor
			{
				JobId = jobId,
				CorrelationId = jobId,
				JobHasCompleted = true
			};

			// Act
			await WaitUntilJobCompletedAsync(jobId);

			// Assert
			_executedJobs.Should().BeEquivalentTo(
				new List<object> { expectedJob }, 
				"no correlation context exists, so the job id should be used when performing the job"
			);
		}

		[Fact]
		public async Task Given_job_is_queued_inside_correlationContext_should_use_correlationId_of_correlation_context()
		{
			const string correlationId = "my-id";
			var expectedJob = new BackgroundTestExecutor
			{
				CorrelationId = correlationId,
				JobHasCompleted = true
			};

			// Act
			await _correlationManager.CorrelateAsync(correlationId,
				async () =>
				{
					await Task.Yield();
					expectedJob.JobId = _client.Enqueue<BackgroundTestExecutor>(job => job.RunAsync(250, null));
				});

			await WaitUntilJobCompletedAsync(expectedJob.JobId);

			// Assert
			_executedJobs.Should().BeEquivalentTo(
				new List<object> { expectedJob },
				"a correlation context exists, so the correlation id should be used when performing the job"
			);
		}

		[Fact]
		public async Task Given_job_is_queued_outside_correlationContext_should_put_correlationId_in_http_header()
		{
			string jobId = _client.Enqueue<BackgroundTestExecutor>(job => job.RunAsync(250, null));

			_mockHttp
				.When(matching => matching.Header(CorrelationHttpHeaders.CorrelationId, jobId))
				.Callback(r => _testOutputHelper.WriteLine("Request sent with correlation id: {0}", jobId))
				.Respond(HttpStatusCode.OK)
				.Verifiable();

			// Act
			await WaitUntilJobCompletedAsync(jobId);

			// Assert
			_mockHttp.Verify();
		}

		[Fact]
		public async Task Given_job_is_queued_inside_correlationContext_should_put_correlationId_in_http_header()
		{
			const string correlationId = "my-id";
			string jobId = null;

			_mockHttp
				.When(matching => matching.Header(CorrelationHttpHeaders.CorrelationId, correlationId))
				.Callback(r => _testOutputHelper.WriteLine("Request sent with correlation id: {0}", correlationId))
				.Respond(HttpStatusCode.OK)
				.Verifiable();

			// Act
			await _correlationManager.CorrelateAsync(correlationId,
				async () =>
				{
					await Task.Yield();
					jobId = _client.Enqueue<BackgroundTestExecutor>(job => job.RunAsync(250, null));
				});

			await WaitUntilJobCompletedAsync(jobId);

			// Assert
			_mockHttp.Verify();
		}

		private async Task WaitUntilJobCompletedAsync(string jobId, int maxWaitInMilliseconds = 5000)
		{
			// Request the server to initialize it and to start processing.
			_services.GetRequiredService<BackgroundJobServer>();

			IMonitoringApi monitoringApi = _jobStorage.GetMonitoringApi();

			Stopwatch sw = Stopwatch.StartNew();
			JobDetailsDto jobDetails = null;
			while ((jobDetails == null || jobDetails.History.All(s => s.StateName != "Succeeded")) && (sw.Elapsed.TotalMilliseconds < maxWaitInMilliseconds || Debugger.IsAttached))
			{
				await Task.Delay(25);
				jobDetails = monitoringApi.JobDetails(jobId);
				if (monitoringApi.FailedCount() > 0)
				{
					break;
				}
			}

			FailedJobDto failedJob = monitoringApi
				.FailedJobs(0, int.MaxValue)
				.Select(j => j.Value)
				.FirstOrDefault();
			if (failedJob != null)
			{
				throw new InvalidOperationException($"Job failed: {failedJob.ExceptionDetails}.");
			}

			_client.Delete(jobId);
		}
	}
}
