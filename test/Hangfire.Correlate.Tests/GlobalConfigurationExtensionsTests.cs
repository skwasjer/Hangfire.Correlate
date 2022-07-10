using FluentAssertions;
using Hangfire.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Hangfire.Correlate;

[Collection(nameof(GlobalTestContext))]
public abstract class GlobalConfigurationExtensionsTests : GlobalTestContext
{
    private readonly Mock<IGlobalConfiguration> _configMock;

    protected GlobalConfigurationExtensionsTests()
    {
        _configMock = new Mock<IGlobalConfiguration>();
    }

    protected abstract void Use(IGlobalConfiguration configuration);

    [Fact]
    public void When_using_it_should_register_filter()
    {
        Use(_configMock.Object);

        GlobalJobFilters.Filters.Should()
            .Contain(f => f.Instance is CorrelateFilterAttribute)
            .Which.Scope.Should().Be(JobFilterScope.Global);
    }

    public class WithLoggerFactory : GlobalConfigurationExtensionsTests
    {
        [Theory]
        [MemberData(nameof(NullArgTestCases))]
        public void Given_that_required_arg_is_null_when_using_it_should_throw
        (
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
            IGlobalConfiguration configuration = Mock.Of<IGlobalConfiguration>();
            ILoggerFactory loggerFactory = Mock.Of<ILoggerFactory>();

            yield return new object?[] { null, loggerFactory, nameof(configuration) };
            yield return new object?[] { configuration, null, nameof(loggerFactory) };
        }

        protected override void Use(IGlobalConfiguration configuration)
        {
            configuration.UseCorrelate(Mock.Of<ILoggerFactory>());
        }
    }

    public class WithServiceProvider : GlobalConfigurationExtensionsTests
    {
        private readonly ServiceProvider _services;

        public WithServiceProvider()
        {
            _services = new ServiceCollection()
                .AddLogging()
                .BuildServiceProvider();
        }

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
                using ServiceProvider? services = new ServiceCollection()
                    .BuildServiceProvider();
                _configMock.Object.UseCorrelate(services);
            };

            // Assert
            act.Should()
                .ThrowExactly<InvalidOperationException>()
                .WithMessage("Failed to register Correlate with Hangfire.*");
        }

        [Theory]
        [MemberData(nameof(NullArgTestCases))]
        public void Given_that_required_arg_is_null_when_using_it_should_throw
        (
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
            IGlobalConfiguration configuration = Mock.Of<IGlobalConfiguration>();
            IServiceProvider serviceProvider = Mock.Of<IServiceProvider>();

            yield return new object?[] { null, serviceProvider, nameof(configuration) };
            yield return new object?[] { configuration, null, nameof(serviceProvider) };
        }

        protected override void Use(IGlobalConfiguration configuration)
        {
            configuration.UseCorrelate(Mock.Of<ILoggerFactory>());
        }
    }
}
