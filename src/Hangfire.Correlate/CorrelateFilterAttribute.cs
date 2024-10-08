﻿using Correlate;
using Hangfire.Client;
using Hangfire.Common;
using Hangfire.Server;
using Hangfire.States;

namespace Hangfire.Correlate;

#pragma warning disable S3993 // Custom attributes should be marked with "System.AttributeUsageAttribute"
internal sealed class CorrelateFilterAttribute
#pragma warning restore S3993 // Custom attributes should be marked with "System.AttributeUsageAttribute"
    : JobFilterAttribute,
      IClientFilter,
      IServerFilter
{
    private const string CorrelationIdKey = "CorrelationId";
    private const string CorrelateActivityKey = "Correlate-Activity";
    private readonly IActivityFactory _activityFactory;

    private readonly ICorrelationContextAccessor _correlationContextAccessor;

    public CorrelateFilterAttribute(ICorrelationContextAccessor correlationContextAccessor, IActivityFactory activityFactory)
    {
        _correlationContextAccessor = correlationContextAccessor ?? throw new ArgumentNullException(nameof(correlationContextAccessor));
        _activityFactory = activityFactory ?? throw new ArgumentNullException(nameof(activityFactory));
    }

    public void OnCreating(CreatingContext filterContext)
    {
        // Assign correlation id to job if job is started in correlation context.
        string? correlationId = _correlationContextAccessor.CorrelationContext?.CorrelationId;
        if (string.IsNullOrEmpty(correlationId))
        {
            correlationId = GetContinuationCorrelationId(filterContext);
        }

        if (!string.IsNullOrEmpty(correlationId))
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
        CorrelationContext? correlationContext = activity.Start(correlationId);
        if (correlationContext == null!)
        {
            return;
        }

        filterContext.Items[CorrelationIdKey] = correlationContext.CorrelationId;
        filterContext.Items[CorrelateActivityKey] = activity;
    }

    public void OnPerformed(PerformedContext filterContext)
    {
        if (filterContext.Items.TryGetValue(CorrelateActivityKey, out object? objActivity)
            && objActivity is IActivity activity)
        {
            activity.Stop();
        }
    }

    /// <summary>
    /// When this is a continuation job, use the correlation id from the parent.
    /// </summary>
    private static string? GetContinuationCorrelationId(CreateContext filterContext)
    {
        if (filterContext.InitialState is not AwaitingState awaitingState)
        {
            return null;
        }

        string parentCorrelationId =
            SerializationHelper.Deserialize<string>(filterContext.Connection.GetJobParameter(awaitingState.ParentId, CorrelationIdKey));
        return string.IsNullOrEmpty(parentCorrelationId)
            ? awaitingState.ParentId
            : parentCorrelationId;
    }
}
