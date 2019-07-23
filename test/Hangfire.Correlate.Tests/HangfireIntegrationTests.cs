using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Correlate;
using Correlate.Http;
using FluentAssertions;
using Hangfire.Server;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using MockHttp;
using Xunit;
using Xunit.Abstractions;
using CorrelationManager = Correlate.CorrelationManager;

namespace Hangfire.Correlate
{
	public abstract class HangfireIntegrationTests : IAsyncLifetime
	{
		private readonly ITestOutputHelper _testOutputHelper;
		private readonly CorrelationManager _correlationManager;

		private IBackgroundProcessingServer _server;
		private IBackgroundJobClient _client;

		protected HangfireIntegrationTests(ITestOutputHelper testOutputHelper)
		{
			// Output type + timestamp
			_testOutputHelper = testOutputHelper ?? throw new ArgumentNullException(nameof(testOutputHelper));
			_testOutputHelper.WriteLine(GetType().Name + "," + DateTime.Now.Ticks);

			MockHttp.Fallback.Respond(HttpStatusCode.OK);

			var correlationContextAccessor = new CorrelationContextAccessor();
			_correlationManager = new CorrelationManager(
				new CorrelationContextFactory(correlationContextAccessor),
				new GuidCorrelationIdFactory(),
				correlationContextAccessor,
				new TestLogger<CorrelationManager>()
			);
		}

		public virtual Task InitializeAsync()
		{
			// We create server so that we can test that jobs are executed properly.
			_server = CreateServer();
			// We create client to enqueue new jobs.
			_client = CreateClient();
			return Task.CompletedTask;
		}

		public virtual Task DisposeAsync()
		{
			_server.Dispose();
			MockHttp.Dispose();
			return Task.CompletedTask;
		}

		protected MockHttpHandler MockHttp { get; } = new MockHttpHandler();
		protected ICollection<object> ExecutedJobs { get; } = new List<object>();

		protected abstract IBackgroundProcessingServer CreateServer();
		protected abstract IBackgroundJobClient CreateClient();

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
			ExecutedJobs.Should().BeEquivalentTo(
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
			ExecutedJobs.Should().BeEquivalentTo(
				new List<object> { expectedJob },
				"a correlation context exists, so the correlation id should be used when performing the job"
			);
		}

		[Fact]
		public async Task Given_job_is_queued_outside_correlationContext_should_put_correlationId_in_http_header()
		{
			string jobId = _client.Enqueue<BackgroundTestExecutor>(job => job.RunAsync(250, null));

			MockHttp
				.When(matching => matching.Header(CorrelationHttpHeaders.CorrelationId, jobId))
				.Callback(r => _testOutputHelper.WriteLine("Request sent with correlation id: {0}", jobId))
				.Respond(HttpStatusCode.OK)
				.Verifiable();

			// Act
			await WaitUntilJobCompletedAsync(jobId);

			// Assert
			MockHttp.Verify();
		}

		[Fact]
		public async Task Given_job_is_queued_inside_correlationContext_should_put_correlationId_in_http_header()
		{
			const string correlationId = "my-id";
			string jobId = null;

			MockHttp
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
			MockHttp.Verify();
		}

		private async Task WaitUntilJobCompletedAsync(string jobId, int maxWaitInMilliseconds = 5000)
		{
			IMonitoringApi monitoringApi = JobStorage.Current.GetMonitoringApi();

			var sw = Stopwatch.StartNew();
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
