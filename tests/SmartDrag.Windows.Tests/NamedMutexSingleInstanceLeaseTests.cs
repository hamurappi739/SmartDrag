using SmartDrag.Windows.Lifetime;
using Xunit;

namespace SmartDrag.Windows.Tests;

public sealed class NamedMutexSingleInstanceLeaseTests
{
    [Fact]
    public void SecondLeaseWithSameName_IsRejectedUntilFirstIsDisposed()
    {
        var name = $"Local\\SmartDrag.Tests.{Guid.NewGuid():N}";
        using var first = NamedMutexSingleInstanceLease.TryAcquire(name);
        using var second = NamedMutexSingleInstanceLease.TryAcquire(name);

        Assert.True(first.IsAcquired);
        Assert.False(second.IsAcquired);
    }
}
