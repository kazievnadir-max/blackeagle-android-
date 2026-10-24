using Android.Content;
using Android.Content.PM;
using Android.Net;

namespace BlackEagle.Android;

internal static class SplitTunnelPreferences
{
    internal sealed record AppEntry(string Package, string Label);

    /// <summary>Читает сохраненные правила отдельно от секретов и настроек Linux.</summary>
    internal static SplitTunnelPolicy Load() => new(
        MobilePreferences.Read("split-enabled", "false") == "true",
        MobilePreferences.Read("split-mode", "exclude") == "include",
        MobilePreferences.Read("split-packages", "").Split('\n', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Возвращает приложения с запускаемым экраном, видимые через ограниченный queries.</summary>
    internal static AppEntry[] Applications()
    {
        var context = global::Android.App.Application.Context;
        var manager = context.PackageManager!;
        using var intent = new Intent(Intent.ActionMain);
        intent.AddCategory(Intent.CategoryLauncher);
        return (manager.QueryIntentActivities(intent, PackageInfoFlags.MatchAll) ?? [])
            .Where(x => x.ActivityInfo?.PackageName is { } name && name != context.PackageName)
            .Select(x => new AppEntry(x.ActivityInfo!.PackageName!, x.LoadLabel(manager)?.ToString() ?? x.ActivityInfo.PackageName!))
            .DistinctBy(x => x.Package).OrderBy(x => x.Label, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    /// <summary>Применяет ровно один режим фильтрации UID до создания интерфейса VPN.</summary>
    internal static void Apply(VpnService.Builder builder)
    {
        var policy = Load();
        if (!policy.Enabled) return;
        var manager = global::Android.App.Application.Context.PackageManager!;
        var installed = new List<string>();
        foreach (var package in policy.Packages)
        {
            try { manager.GetApplicationInfo(package, 0); installed.Add(package); }
            catch (PackageManager.NameNotFoundException) { }
        }
        foreach (var package in policy.Resolve(installed))
        {
            if (policy.IncludeOnly) builder.AddAllowedApplication(package);
            else builder.AddDisallowedApplication(package);
        }
    }
}
