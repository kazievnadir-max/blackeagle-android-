using Xunit;

namespace BlackEagle.Android.Tests;

public sealed class SplitTunnelPolicyTests
{
    /// <summary>Отключенная настройка не меняет маршрутизацию даже с пустым списком.</summary>
    [Fact]
    public void DisabledReturnsNoRules() => Assert.Empty(new SplitTunnelPolicy(false, true, []).Resolve([]));

    /// <summary>Разрешающий режим не превращается в полный VPN после удаления выбранного приложения.</summary>
    [Fact]
    public void MissingAllowedAppsFailClosed() => Assert.Throws<InvalidOperationException>(
        () => new SplitTunnelPolicy(true, true, ["removed.app"]).Resolve(["installed.app"]));

    /// <summary>Оба режима отбрасывают удаленные пакеты и повторения.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void KeepsInstalledPackagesOnly(bool includeOnly) => Assert.Equal(["installed.app"],
        new SplitTunnelPolicy(true, includeOnly, ["installed.app", "removed.app", "installed.app"]).Resolve(["installed.app"]));

    /// <summary>Пустой список исключений означает обычный VPN для всех приложений.</summary>
    [Fact]
    public void EmptyExclusionsAreValid() => Assert.Empty(new SplitTunnelPolicy(true, false, []).Resolve([]));
}
