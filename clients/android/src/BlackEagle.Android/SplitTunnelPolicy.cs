namespace BlackEagle.Android;

internal sealed record SplitTunnelPolicy(bool Enabled, bool IncludeOnly, string[] Packages)
{
    /// <summary>Отбирает установленные приложения и запрещает пустой разрешающий список.</summary>
    public string[] Resolve(IEnumerable<string> installed)
    {
        if (!Enabled) return [];
        var available = installed.ToHashSet(StringComparer.Ordinal);
        var result = Packages.Where(available.Contains).Distinct(StringComparer.Ordinal).ToArray();
        if (IncludeOnly && result.Length == 0)
            throw new InvalidOperationException("Выберите хотя бы одно установленное приложение для VPN.");
        return result;
    }
}
