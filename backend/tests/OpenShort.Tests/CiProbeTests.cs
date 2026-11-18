namespace OpenShort.Tests;

// Temporary: checks that a failing test turns the pull request red. Reverted in the next commit.
public class CiProbeTests
{
    [Test]
    public void Probe_ShouldFail() => Assert.Fail("Temporary failure to verify the CI workflow");
}
