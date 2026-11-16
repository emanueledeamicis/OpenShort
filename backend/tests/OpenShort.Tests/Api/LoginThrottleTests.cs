using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenShort.Api.Auth;

namespace OpenShort.Tests.Api;

[TestFixture]
public class LoginThrottleTests
{
    private const string Identifier = "admin";
    private const string Client = "203.0.113.10";
    private const string OtherClient = "203.0.113.20";

    private ManualTimeProvider _timeProvider = null!;
    private LoginThrottle _throttle = null!;
    private int _verifications;

    [SetUp]
    public void Setup()
    {
        _timeProvider = new ManualTimeProvider();
        _throttle = new LoginThrottle(_timeProvider, NullLogger<LoginThrottle>.Instance);
        _verifications = 0;
    }

    [TestCase(0, 0)]
    [TestCase(1, 0)]
    [TestCase(2, 0)]
    [TestCase(3, 1)]
    [TestCase(4, 2)]
    [TestCase(5, 4)]
    [TestCase(12, 512)]
    [TestCase(13, 900)]
    [TestCase(100, 900)]
    [TestCase(int.MaxValue, 900)]
    public void GetDelay_ShouldGrowWithFailures_UpToTheMaximum(int failures, int expectedSeconds)
    {
        // Act
        var delay = LoginThrottle.GetDelay(failures);

        // Assert
        delay.Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Test]
    public async Task AttemptAsync_ShouldNotDelay_FirstFailures()
    {
        // Act
        var results = new List<LoginAttemptResult>();
        for (var attempt = 0; attempt < LoginThrottle.FailuresWithoutDelay + 1; attempt++)
        {
            results.Add(await FailAsync());
        }

        // Assert
        results.Should().AllSatisfy(result => result.Should().Be(LoginAttemptResult.Failure));
        _verifications.Should().Be(LoginThrottle.FailuresWithoutDelay + 1);
    }

    [Test]
    public async Task AttemptAsync_ShouldDelayWithoutVerifying_AfterRepeatedFailures()
    {
        // Arrange
        await FailAsync(times: 3);

        // Act
        var result = await SucceedAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.RetryAfter.Should().Be(TimeSpan.FromSeconds(1));
        _verifications.Should().Be(3);
    }

    [Test]
    public async Task AttemptAsync_ShouldVerifyAgain_WhenTheDelayHasElapsed()
    {
        // Arrange
        await FailAsync(times: 3);
        _timeProvider.Advance(TimeSpan.FromSeconds(1));

        // Act
        var result = await SucceedAsync();

        // Assert
        result.Should().Be(LoginAttemptResult.Success);
    }

    [Test]
    public async Task AttemptAsync_ShouldIncreaseTheDelay_AtEveryFurtherFailure()
    {
        // Arrange
        await FailAsync(times: 2);

        // Act
        var delays = new List<TimeSpan?>();
        for (var attempt = 0; attempt < 4; attempt++)
        {
            await FailAsync();
            var delayed = await FailAsync();
            delays.Add(delayed.RetryAfter);
            _timeProvider.Advance(delayed.RetryAfter!.Value);
        }

        // Assert
        delays.Should().Equal(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(4),
            TimeSpan.FromSeconds(8));
    }

    [Test]
    public async Task AttemptAsync_ShouldNotDelayOtherClients_OnTheSameIdentifier()
    {
        // Arrange
        await FailAsync(times: 10);

        // Act
        var result = await SucceedAsync(client: OtherClient);

        // Assert
        result.Should().Be(LoginAttemptResult.Success);
    }

    [Test]
    public async Task AttemptAsync_ShouldNotDelayOtherIdentifiers_FromTheSameClient()
    {
        // Arrange
        await FailAsync(times: 10);

        // Act
        var result = await SucceedAsync(identifier: "someone-else");

        // Assert
        result.Should().Be(LoginAttemptResult.Success);
    }

    [Test]
    public async Task AttemptAsync_ShouldIgnoreCaseAndSpaces_InTheIdentifier()
    {
        // Arrange
        await FailAsync(times: 3);

        // Act
        var result = await SucceedAsync(identifier: " ADMIN ");

        // Assert
        result.RetryAfter.Should().NotBeNull();
    }

    [Test]
    public async Task AttemptAsync_ShouldResetFailures_WhenAttemptSucceeds()
    {
        // Arrange
        await FailAsync(times: 2);
        await SucceedAsync();

        // Act
        var results = new List<LoginAttemptResult>();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            results.Add(await FailAsync());
        }

        // Assert
        results.Should().AllSatisfy(result => result.Should().Be(LoginAttemptResult.Failure));
    }

    [Test]
    public async Task AttemptAsync_ShouldForgetFailuresWithoutDelay_AfterTheIdlePeriod()
    {
        // Arrange
        await FailAsync(times: 2);
        _timeProvider.Advance(LoginThrottle.ForgetAfter);

        // Act
        var results = new List<LoginAttemptResult>();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            results.Add(await FailAsync());
        }

        // Assert
        results.Should().AllSatisfy(result => result.Should().Be(LoginAttemptResult.Failure));
    }

    [Test]
    public async Task AttemptAsync_ShouldKeepDelayedFailures_AfterTheIdlePeriod()
    {
        // Arrange
        await FailAsync(times: 3);
        _timeProvider.Advance(LoginThrottle.GetDelay(3) + LoginThrottle.ForgetAfter);

        // Act
        await FailAsync();
        var result = await SucceedAsync();

        // Assert
        result.RetryAfter.Should().Be(LoginThrottle.GetDelay(4));
    }

    [Test]
    public async Task AttemptAsync_ShouldForgetDelayedFailures_AfterTheLongIdlePeriod()
    {
        // Arrange
        await FailAsync(times: 3);
        _timeProvider.Advance(LoginThrottle.GetDelay(3) + LoginThrottle.ForgetDelayedAfter);

        // Act
        var results = new List<LoginAttemptResult>();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            results.Add(await FailAsync());
        }

        // Assert
        results.Should().AllSatisfy(result => result.Should().Be(LoginAttemptResult.Failure));
    }

    [Test]
    public async Task AttemptAsync_ShouldCountEveryFailure_WhenAttemptsRunInParallel()
    {
        // Act
        var attempts = Enumerable.Range(0, 40)
            .Select(_ => Task.Run(() => _throttle.AttemptAsync(Identifier, Client, async () =>
            {
                Interlocked.Increment(ref _verifications);
                await Task.Delay(5);
                return false;
            })))
            .ToList();
        var results = await Task.WhenAll(attempts);

        // Assert
        _verifications.Should().Be(LoginThrottle.FailuresWithoutDelay + 1);
        results.Count(result => result.RetryAfter == null).Should().Be(LoginThrottle.FailuresWithoutDelay + 1);
        results.Count(result => result.RetryAfter != null).Should().Be(40 - LoginThrottle.FailuresWithoutDelay - 1);
    }

    [Test]
    public async Task AttemptAsync_ShouldNotCountAFailure_WhenVerificationThrows()
    {
        // Arrange
        await FailAsync(times: 2);

        // Act
        var act = () => _throttle.AttemptAsync(Identifier, Client, () => throw new InvalidOperationException());
        await act.Should().ThrowAsync<InvalidOperationException>();
        var result = await FailAsync();

        // Assert
        result.Should().Be(LoginAttemptResult.Failure);
    }

    [Test]
    public async Task Reset_ShouldRemoveThePendingDelay()
    {
        // Arrange
        await FailAsync(times: 5);

        // Act
        _throttle.Reset(Identifier, Client);
        var result = await SucceedAsync();

        // Assert
        result.Should().Be(LoginAttemptResult.Success);
    }

    [Test]
    public async Task AttemptAsync_ShouldStopTrackingNewClients_WhenTheLimitIsReached()
    {
        // Arrange
        await FillTrackedClientsAsync();

        // Act
        await FailAsync(times: 5, client: OtherClient);
        var untrackedClient = await SucceedAsync(client: OtherClient);
        await FailAsync(times: 2, client: "client-0");
        var trackedClient = await SucceedAsync(client: "client-0");

        // Assert
        untrackedClient.Should().Be(LoginAttemptResult.Success);
        trackedClient.RetryAfter.Should().NotBeNull();
    }

    [Test]
    public async Task AttemptAsync_ShouldTrackNewClientsAgain_WhenOldEntriesHaveExpired()
    {
        // Arrange
        await FillTrackedClientsAsync();
        _timeProvider.Advance(LoginThrottle.ForgetAfter);

        // Act
        await FailAsync(times: 3, client: OtherClient);
        var result = await SucceedAsync(client: OtherClient);

        // Assert
        result.RetryAfter.Should().NotBeNull();
    }

    private async Task FillTrackedClientsAsync()
    {
        for (var client = 0; client < LoginThrottle.MaxTrackedClients; client++)
        {
            await FailAsync(client: $"client-{client}");
        }
    }

    private Task<LoginAttemptResult> SucceedAsync(string identifier = Identifier, string client = Client)
    {
        return AttemptAsync(identifier, client, succeeds: true);
    }

    private async Task<LoginAttemptResult> FailAsync(int times = 1, string client = Client)
    {
        var result = default(LoginAttemptResult);
        for (var attempt = 0; attempt < times; attempt++)
        {
            result = await AttemptAsync(Identifier, client, succeeds: false);
        }

        return result;
    }

    private Task<LoginAttemptResult> AttemptAsync(string identifier, string client, bool succeeds)
    {
        return _throttle.AttemptAsync(identifier, client, () =>
        {
            _verifications++;
            return Task.FromResult(succeeds);
        });
    }
}
