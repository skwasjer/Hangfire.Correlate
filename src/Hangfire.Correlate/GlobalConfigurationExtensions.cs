using Correlate;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hangfire.Correlate;

/// <summary>
/// Extensions for <see cref="IGlobalConfiguration" />
/// </summary>
public static class GlobalConfigurationExtensions
{
    /// <summary>
    /// Use Correlate with Hangfire to manage the correlation context.
    /// </summary>
    /// <param name="configuration">The global configuration.</param>
    /// <param name="serviceProvider">The service provider with Correlate dependencies registered.</param>
    public static IGlobalConfiguration UseCorrelate(this IGlobalConfiguration configuration, IServiceProvider serviceProvider)
    {
        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        if (serviceProvider is null)
        {
            throw new ArgumentNullException(nameof(serviceProvider));
        }

        try
        {
            return configuration.UseFilter(ActivatorUtilities.CreateInstance<CorrelateFilterAttribute>(serviceProvider));
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException("Failed to register Correlate with Hangfire. Please ensure `.AddCorrelate()` is called on the service collection.", ex);
        }
    }

    /// <summary>
    /// Use Correlate with Hangfire to manage the correlation context.
    /// </summary>
    /// <param name="configuration">The global configuration.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public static IGlobalConfiguration UseCorrelate(this IGlobalConfiguration configuration, ILoggerFactory loggerFactory)
    {
        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        if (loggerFactory is null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }

        var correlationContextAccessor = new CorrelationContextAccessor();
        var correlationManager = new CorrelationManager(
            new CorrelationContextFactory(correlationContextAccessor),
            new GuidCorrelationIdFactory(),
            correlationContextAccessor,
            loggerFactory.CreateLogger<CorrelationManager>()
        );
        var correlateFilterAttribute = new CorrelateFilterAttribute(correlationContextAccessor, correlationManager);
        return configuration.UseFilter(correlateFilterAttribute);
    }
}
