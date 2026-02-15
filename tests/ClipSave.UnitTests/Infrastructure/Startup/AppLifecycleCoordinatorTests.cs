using ClipSave.Infrastructure.Startup;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Windows.Threading;

namespace ClipSave.UnitTests;

public class AppLifecycleCoordinatorTests
{
    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var coordinator = CreateCoordinator();

        var act = () =>
        {
            coordinator.Dispose();
            coordinator.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void TryStartPrimaryInstance_AfterDispose_ThrowsObjectDisposedException()
    {
        var coordinator = CreateCoordinator();
        coordinator.Dispose();

        var act = () => coordinator.TryStartPrimaryInstance();

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Constructor_NullServiceProvider_ThrowsArgumentNullException()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var logger = NullLogger<AppLifecycleCoordinator>.Instance;

        var act = () => new AppLifecycleCoordinator(
            null!,
            dispatcher,
            logger,
            () => { });

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("serviceProvider");
    }

    [Fact]
    public void Constructor_NullDispatcher_ThrowsArgumentNullException()
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var logger = NullLogger<AppLifecycleCoordinator>.Instance;

        var act = () => new AppLifecycleCoordinator(
            serviceProvider,
            null!,
            logger,
            () => { });

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("dispatcher");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var dispatcher = Dispatcher.CurrentDispatcher;

        var act = () => new AppLifecycleCoordinator(
            serviceProvider,
            dispatcher,
            null!,
            () => { });

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_NullShutdownAction_ThrowsArgumentNullException()
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var dispatcher = Dispatcher.CurrentDispatcher;
        var logger = NullLogger<AppLifecycleCoordinator>.Instance;

        var act = () => new AppLifecycleCoordinator(
            serviceProvider,
            dispatcher,
            logger,
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("shutdownApplication");
    }

    private static AppLifecycleCoordinator CreateCoordinator()
    {
        return new AppLifecycleCoordinator(
            new ServiceCollection().BuildServiceProvider(),
            Dispatcher.CurrentDispatcher,
            NullLogger<AppLifecycleCoordinator>.Instance,
            () => { });
    }
}
