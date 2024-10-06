namespace Hangfire.Correlate;

/// <summary>
/// Marker to disable parallel tests because when registering Hangfire filters, it adds it to a static list.
/// This type should also be used a base class for tests setting the filter so that it can be cleaned up each time.
/// </summary>
[CollectionDefinition(nameof(GlobalTestContext), DisableParallelization = true)]
public abstract class GlobalTestContext : IAsyncLifetime
{
    public virtual Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public virtual Task DisposeAsync()
    {
        CleanUpFilters();
        return Task.CompletedTask;
    }

    private static void CleanUpFilters()
    {
        foreach (object? filter in GlobalJobFilters.Filters
            .Where(f => f.Instance is CorrelateFilterAttribute)
            .Select(f => f.Instance)
            .ToArray())
        {
            GlobalJobFilters.Filters.Remove(filter);
        }
    }
}
