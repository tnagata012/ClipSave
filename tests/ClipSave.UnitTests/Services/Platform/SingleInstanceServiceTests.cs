using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipSave.UnitTests;

[UnitTest]
public class SingleInstanceServiceTests : IDisposable
{
    private readonly List<SingleInstanceService> _services = [];
    private readonly string _testId = Guid.NewGuid().ToString("N");

    public void Dispose()
    {
        foreach (var service in _services)
        {
            service.Dispose();
        }
    }

    private SingleInstanceService CreateService()
    {
        var logger = Mock.Of<ILogger<SingleInstanceService>>();
        var mutexName = $"Local\\ClipSave_Test_{_testId}";
        var pipeName = $"ClipSave_Test_{_testId}";
        var service = new SingleInstanceService(logger, mutexName, pipeName);
        _services.Add(service);
        return service;
    }

    [Fact]
    public void TryAcquireOrNotify_FirstInstance_ReturnsTrue()
    {
        var service = CreateService();

        var result = service.TryAcquireOrNotify();

        result.Should().BeTrue("the first instance should start as primary");
    }

    [Fact]
    public void TryAcquireOrNotify_SecondInstance_ReturnsFalse()
    {
        var firstService = CreateService();
        firstService.TryAcquireOrNotify();

        var secondService = CreateService();
        var result = secondService.TryAcquireOrNotify();

        result.Should().BeFalse("the second instance should fail when another instance exists");
    }

    [Fact]
    public async Task TryAcquireOrNotify_SecondInstance_RaisesSecondInstanceLaunched()
    {
        var firstService = CreateService();
        firstService.TryAcquireOrNotify();

        var eventRaised = new TaskCompletionSource<bool>();
        firstService.SecondInstanceLaunched += (_, _) => eventRaised.TrySetResult(true);

        var secondService = CreateService();
        secondService.TryAcquireOrNotify();

        var result = await Task.WhenAny(
            eventRaised.Task,
            Task.Delay(TimeSpan.FromSeconds(5)));

        result.Should().Be(eventRaised.Task, "starting a second instance should raise SecondInstanceLaunched");
        (await eventRaised.Task).Should().BeTrue();
    }

    [Fact]
    public void TryAcquireOrNotify_AfterFirstInstanceDisposed_NewInstanceCanBePrimary()
    {
        var firstService = CreateService();
        firstService.TryAcquireOrNotify();

        firstService.Dispose();
        _services.Remove(firstService);

        var newService = CreateService();
        var result = newService.TryAcquireOrNotify();

        result.Should().BeTrue("after the previous instance exits, a new instance should become primary");
    }

    [Fact]
    public void BuildScopedMutexName_IncludesUserAndSessionScope()
    {
        var name = SingleInstanceService.BuildScopedMutexName("S-1-5-21-1234", sessionId: 2);

        name.Should().Be("Local\\ClipSave_SingleInstance_S_1_5_21_1234_s2");
    }

    [Fact]
    public void BuildScopedPipeName_UsesSanitizedFallbackWhenScopeIsMissing()
    {
        var name = SingleInstanceService.BuildScopedPipeName("", sessionId: null);

        name.Should().StartWith("ClipSave_SingleInstancePipe_unknown_s");
    }
}
