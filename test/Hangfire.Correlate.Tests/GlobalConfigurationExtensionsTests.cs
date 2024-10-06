using Correlate;
using Hangfire.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hangfire.Correlate;

[Collection(nameof(GlobalTestContext))]
public abstract class GlobalConfigurationExtensionsTests : GlobalTestContext
{
    private readonly IGlobalConfiguration _configMock = Substitute.For<IGlobalConfiguration>();

    protected abstract void Use(IGlobalConfiguration configuration);

    [Fact]
    public void When_using_it_should_register_filter()
    {
        GlobalJobFilters.Filters.Should()
            .NotContain(f => f.Instance is CorrelateFilterAttribute);

        Use(_configMock);

        GlobalJobFilters.Filters.Should()
            .Contain(f => f.Instance is CorrelateFilterAttribute)
            .Which.Scope.Should()
            .Be(JobFilterScope.Global);
    }

    [Obsolete("To be removed in next major release.")]
    public class WithLoggerFactory : GlobalConfigurationExtensionsTests
    {
        [Theory]
        [MemberData(nameof(NullArgTestCases))]
        public void Given_that_required_arg_is_null_when_using_it_should_throw(
            IGlobalConfiguration configuration,
            ILoggerFactory loggerFactory,
            string expectedParamName
        )
        {
            Action act = () => configuration.UseCorrelate(loggerFactory);

            act.Should()
                .ThrowExactly<ArgumentNullException>()
                .WithParameterName(expectedParamName);
        }

        public static IEnumerable<object?[]> NullArgTestCases()
        {
            IGlobalConfiguration configuration = Substitute.For<IGlobalConfiguration>();
            ILoggerFactory loggerFactory = Substitute.For<ILoggerFactory>();

            yield return new object?[]
            {
                null, loggerFactory, nameof(configuration)
            };
            yield return new object?[]
            {
                configuration, null, nameof(loggerFactory)
            };
        }

        protected override void Use(IGlobalConfiguration configuration)
        {
            configuration.UseCorrelate(Substitute.For<ILoggerFactory>());
        }
    }

    public class WithServiceProvider : GlobalConfigurationExtensionsTests
    {
        private readonly ServiceProvider _services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        public override async Task DisposeAsync()
        {
            await _services.DisposeAsync();
            await base.DisposeAsync();
        }

        [Fact]
        public void Given_that_required_services_are_not_registered_when_using_it_should_throw()
        {
            Action act = () =>
            {
                using ServiceProvider services = new ServiceCollection()
                    .BuildServiceProvider();
                _configMock.UseCorrelate(services);
            };

            // Assert
            act.Should()
                .ThrowExactly<InvalidOperationException>()
                .WithMessage("Failed to register Correlate with Hangfire.*");
        }

        [Theory]
        [MemberData(nameof(NullArgTestCases))]
        public void Given_that_required_arg_is_null_when_using_it_should_throw(
            IGlobalConfiguration configuration,
            IServiceProvider serviceProvider,
            string expectedParamName
        )
        {
            Action act = () => configuration.UseCorrelate(serviceProvider);

            act.Should()
                .ThrowExactly<ArgumentNullException>()
                .WithParameterName(expectedParamName);
        }

        public static IEnumerable<object?[]> NullArgTestCases()
        {
            IGlobalConfiguration configuration = Substitute.For<IGlobalConfiguration>();
            IServiceProvider serviceProvider = Substitute.For<IServiceProvider>();

            yield return new object?[]
            {
                null, serviceProvider, nameof(configuration)
            };
            yield return new object?[]
            {
                configuration, null, nameof(serviceProvider)
            };
        }

        protected override void Use(IGlobalConfiguration configuration)
        {
            IServiceProvider serviceProviderMock = Substitute.For<IServiceProvider>();
            serviceProviderMock
                .GetService(typeof(ICorrelationContextAccessor))
                .Returns(Substitute.For<ICorrelationContextAccessor>());
            serviceProviderMock
                .GetService(typeof(IActivityFactory))
                .Returns(Substitute.For<IActivityFactory>());
            configuration.UseCorrelate(serviceProviderMock);
        }
    }
}
