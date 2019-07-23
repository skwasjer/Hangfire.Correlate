using System;
using Correlate;
using Hangfire.Client;
using Hangfire.Common;
using Hangfire.Server;

namespace Hangfire.Correlate
{
	internal class CorrelateFilterAttribute : JobFilterAttribute, IClientFilter, IServerFilter
	{
		private const string CorrelationIdKey = "CorrelationId";
		private const string CorrelateActivityKey = "Correlate-Activity";

		private readonly ICorrelationContextAccessor _correlationContextAccessor;
		private readonly IActivityFactory _activityFactory;

		public CorrelateFilterAttribute(ICorrelationContextAccessor correlationContextAccessor, IActivityFactory activityFactory)
		{
			_correlationContextAccessor = correlationContextAccessor ?? throw new ArgumentNullException(nameof(correlationContextAccessor));
			_activityFactory = activityFactory ?? throw new ArgumentNullException(nameof(activityFactory));
		}

		public void OnCreating(CreatingContext filterContext)
		{
			// Assign correlation id to job if job is started in correlation context.
			string correlationId = _correlationContextAccessor.CorrelationContext?.CorrelationId;
			if (!string.IsNullOrWhiteSpace(correlationId))
			{
				filterContext.SetJobParameter(CorrelationIdKey, correlationId);
			}
		}

		public void OnCreated(CreatedContext filterContext)
		{
		}

		public void OnPerforming(PerformingContext filterContext)
		{
			string correlationId = filterContext.GetJobParameter<string>(CorrelationIdKey) ?? filterContext.BackgroundJob.Id;
			IActivity activity = _activityFactory.CreateActivity();
			CorrelationContext correlationContext = activity.Start(correlationId);
			if (correlationContext != null)
			{
				filterContext.Items[CorrelationIdKey] = correlationContext.CorrelationId;
				filterContext.Items[CorrelateActivityKey] = activity;
			}
		}

		public void OnPerformed(PerformedContext filterContext)
		{
			if (filterContext.Items.TryGetValue(CorrelateActivityKey, out object objActivity)
				&& objActivity is IActivity activity)
			{
				activity.Stop();
			}
		}
	}
}
