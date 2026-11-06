using TeamPortal.Mock;

namespace TeamPortal.Services.Tests;

public class MockOptionsTests
{
    [Fact]
    public void TestInfrastructure_IsWiredUp()
    {
        true.Should().BeTrue();
    }

    [Fact]
    public void MockOptions_ByDefault_SimulatesLatency()
    {
        // Deliberate, and load-bearing. A UI built against instant reads never
        // grows a loading state, and one built against instant writes never
        // disables its Save button. See Frontend/BuildPlan.md, rule 3.
        var options = new MockOptions();

        options.ReadDelay.Should().BeGreaterThan(TimeSpan.Zero);
        options.WriteDelay.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void MockOptions_Instant_HasNoDelays()
    {
        MockOptions options = MockOptions.Instant;

        options.ReadDelay.Should().Be(TimeSpan.Zero);
        options.WriteDelay.Should().Be(TimeSpan.Zero, "no test suite should sleep");
    }

    [Fact]
    public void MockOptions_FailNextWrite_DefaultsToOff()
    {
        new MockOptions().FailNextWrite.Should().BeFalse();
    }

    [Fact]
    public void MockOptions_FailNextWrite_IsConsumedOnce()
    {
        var options = new MockOptions { FailNextWrite = true };

        options.ConsumeFailureFlag().Should().BeTrue("the flag was set");
        options.ConsumeFailureFlag().Should().BeFalse("it is a one-shot switch, not a mode");
    }

    [Fact]
    public void MockOptions_WhenFlagIsOff_ConsumeReturnsFalse()
    {
        new MockOptions().ConsumeFailureFlag().Should().BeFalse();
    }
}
