using Android.Content;
using Android.OS;
using BlackEagle.Client.UI;

namespace BlackEagle.Android;

internal sealed class AndroidMobileViewPlatform : IMobileViewPlatform
{
    public bool IsConnected => AndroidTunnel.Instance.State == "connected";
    public TimeSpan? ConnectedDuration => AndroidTunnel.Instance.ConnectedAtMilliseconds is { } start
        ? TimeSpan.FromMilliseconds(Math.Max(0, SystemClock.ElapsedRealtime() - start)) : null;
    public string AppVersion => global::Android.App.Application.Context.PackageManager!
        .GetPackageInfo(global::Android.App.Application.Context.PackageName!, 0)!.VersionName ?? "";
    public bool SupportsAutostart => false;
    public bool IsAutostartEnabled => false;

    /// <summary>Сообщает об отсутствии настройки desktop-автозапуска на Android.</summary>
    public bool SetAutostartEnabled(bool enabled) => false;

    /// <summary>Открывает официальный Telegram-бот системным приложением Android.</summary>
    public void OpenTelegram()
    {
        try
        {
            var intent = new Intent(Intent.ActionView, global::Android.Net.Uri.Parse("https://t.me/BlackEagleVPNbot"));
            intent.AddFlags(ActivityFlags.NewTask);
            global::Android.App.Application.Context.StartActivity(intent);
        }
        catch (ActivityNotFoundException exception) { throw new InvalidOperationException("No browser.", exception); }
    }
}
