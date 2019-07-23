using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Correlate;
using Hangfire.Server;
using Xunit.Abstractions;

namespace Hangfire.Correlate
{
	public class BackgroundTestExecutor
	{
		private readonly TestService _testService;
		private readonly ICorrelationContextAccessor _correlationContextAccessor;
		private readonly ITestOutputHelper _testOutputHelper;

		internal BackgroundTestExecutor()
		{
		}

		public BackgroundTestExecutor(TestService testService, ICorrelationContextAccessor correlationContextAccessor, ICollection<object> jobState, ITestOutputHelper testOutputHelper)
		{
			_testService = testService ?? throw new ArgumentNullException(nameof(testService));
			_correlationContextAccessor = correlationContextAccessor ?? throw new ArgumentNullException(nameof(correlationContextAccessor));
			_testOutputHelper = testOutputHelper ?? throw new ArgumentNullException(nameof(testOutputHelper));

			jobState.Add(this);
		}

		/// <summary>
		/// Gets the job id.
		/// </summary>
		public string JobId { get; set; }

		/// <summary>
		/// Gets whether the job has completed.
		/// </summary>
		public bool JobHasCompleted { get; set; }

		/// <summary>
		/// Gets the correlation id that was in the correlation context while the job was running.
		/// </summary>
		public string CorrelationId { get; set; }

		[AutomaticRetry(Attempts = 0)]
		public async Task RunAsync(int timeoutInMillis, PerformContext performContext)
		{
			JobId = performContext.BackgroundJob.Id;
			CorrelationId = _correlationContextAccessor.CorrelationContext.CorrelationId;
			_testOutputHelper.WriteLine("Executing job {0} with correlation id {1}", JobId, CorrelationId);
			await Task.Delay(timeoutInMillis / 2);
			await _testService.CallApi();
			await Task.Delay(timeoutInMillis / 2);
			JobHasCompleted = true;
		}
	}
}