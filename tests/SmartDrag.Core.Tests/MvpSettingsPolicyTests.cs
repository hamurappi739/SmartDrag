using SmartDrag.Core.Output;
using SmartDrag.Core.Settings;
using Xunit;

namespace SmartDrag.Core.Tests;

public sealed class MvpSettingsPolicyTests
{
    [Fact]
    public void Defaults_AreValid() => Assert.True(MvpSettingsPolicy.Validate(new UserSettings()).IsValid);

    [Fact]
    public void PreserveSourceFalse_IsRejected()
    {
        var settings = new UserSettings { Output = new OutputSettings { PreserveSource = false } };
        var result = MvpSettingsPolicy.Validate(settings);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("PreserveSource", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5000)]
    public void ActivationDelayOutsideGuardrails_IsRejected(int value)
    {
        var settings = new UserSettings { Drag = new DragBehaviorSettings { ActivationDelayMs = value } };
        Assert.False(MvpSettingsPolicy.Validate(settings).IsValid);
    }

    [Fact]
    public void NonFiniteOverlayOffset_IsRejected()
    {
        var settings = new UserSettings { Overlay = new OverlaySettings { CursorOffsetDip = double.NaN } };
        Assert.False(MvpSettingsPolicy.Validate(settings).IsValid);
    }

    [Fact]
    public void OutputFactory_AlwaysReturnsNonDestructiveMvpPolicy()
    {
        var policy = MvpOutputPolicyFactory.Create(new UserSettings());
        Assert.True(policy.PreserveSource);
        Assert.Equal(OutputLocationMode.SameDirectory, policy.LocationMode);
        Assert.Equal(CollisionPolicy.GenerateUniqueName, policy.CollisionPolicy);
    }
}
