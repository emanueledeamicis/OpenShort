using FluentAssertions;
using OpenShort.Api.Auth;

namespace OpenShort.Tests.Api;

[TestFixture]
public class UsedTwoFactorCodesTests
{
    private const string UserId = "user-1";
    private const string OtherUserId = "user-2";
    private const string Code = "123456";

    private ManualTimeProvider _timeProvider = null!;
    private UsedTwoFactorCodes _usedCodes = null!;

    [SetUp]
    public void Setup()
    {
        _timeProvider = new ManualTimeProvider();
        _usedCodes = new UsedTwoFactorCodes(_timeProvider);
    }

    [Test]
    public void TryUse_ShouldAcceptACodeOnce()
    {
        // Act
        var first = _usedCodes.TryUse(UserId, Code);
        var second = _usedCodes.TryUse(UserId, Code);

        // Assert
        first.Should().BeTrue();
        second.Should().BeFalse();
    }

    [Test]
    public void TryUse_ShouldAcceptAnotherCodeOfTheSameUser()
    {
        // Arrange
        _usedCodes.TryUse(UserId, Code);

        // Act
        var result = _usedCodes.TryUse(UserId, "654321");

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void TryUse_ShouldAcceptTheSameCode_FromAnotherUser()
    {
        // Arrange
        _usedCodes.TryUse(UserId, Code);

        // Act
        var result = _usedCodes.TryUse(OtherUserId, Code);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void TryUse_ShouldRememberACode_UntilTheApplicationNoLongerAcceptsIt()
    {
        // Arrange
        _usedCodes.TryUse(UserId, Code);
        _timeProvider.Advance(UsedTwoFactorCodes.RememberFor - TimeSpan.FromSeconds(1));

        // Act
        var result = _usedCodes.TryUse(UserId, Code);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void TryUse_ShouldForgetACode_WhenItIsNoLongerRemembered()
    {
        // Arrange: the code of another user is used in the meantime, which is when old entries are removed
        _usedCodes.TryUse(UserId, Code);
        _timeProvider.Advance(UsedTwoFactorCodes.RememberFor);
        _usedCodes.TryUse(OtherUserId, Code);

        // Act
        var result = _usedCodes.TryUse(UserId, Code);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void TryUse_ShouldAcceptACodeOnce_WhenItIsSentInParallel()
    {
        // Arrange
        var accepted = 0;

        // Act
        Parallel.For(0, 64, _ =>
        {
            if (_usedCodes.TryUse(UserId, Code))
            {
                Interlocked.Increment(ref accepted);
            }
        });

        // Assert
        accepted.Should().Be(1);
    }
}
