using System;

namespace Hangfire.Correlate
{
	/// <summary>
	/// Simple activator to create <see cref="BackgroundTestExecutor"/> with dependencies. Only for unit tests that don't use DI.
	/// </summary>
	internal class BackgroundTestExecutorJobActivator : JobActivator
	{
		private readonly Func<BackgroundTestExecutor> _createJob;

		public BackgroundTestExecutorJobActivator(Func<BackgroundTestExecutor> createJob)
		{
			_createJob = createJob;
		}

		public override object ActivateJob(Type jobType)
		{
			if (jobType != typeof(BackgroundTestExecutor))
			{
				throw new NotSupportedException();
			}

			return _createJob();
		}
	}
}
