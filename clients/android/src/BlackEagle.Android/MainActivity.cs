using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;
using Android.Content;
using Android.Net;

namespace BlackEagle.Android;

[Activity(
    Label = "@string/app_name",
    Theme = "@style/BlackEagleTheme",
    Icon = "@drawable/eagle_head_black",
    MainLauncher = true,
    Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity<App>
{
    private const int VpnPermissionRequest = 4101;
    private static MainActivity? current;
    internal static bool IsReady => current is not null;
    private static TaskCompletionSource<bool>? permission;

    /// <summary>Запоминает активный экран для системного запроса разрешения VPN.</summary>
    protected override void OnResume()
    {
        base.OnResume(); current = this;
        Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
        {
            if (global::Avalonia.Application.Current is App app) await app.HandleTileRequestAsync();
        });
    }

    /// <summary>Запрашивает разрешение VPN на UI-потоке и ожидает решения пользователя.</summary>
    internal static async Task RequestVpnPermissionAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (VpnService.Prepare(global::Android.App.Application.Context) is null) return;
        var activity = current ?? throw new InvalidOperationException("Откройте приложение для подключения VPN.");
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        activity.RunOnUiThread(() =>
        {
            try
            {
                if (ct.IsCancellationRequested) { completion.TrySetCanceled(ct); return; }
                var intent = VpnService.Prepare(activity);
                if (intent is null) { completion.TrySetResult(true); return; }
                permission = completion;
#pragma warning disable CS0618
                activity.StartActivityForResult(intent, VpnPermissionRequest);
#pragma warning restore CS0618
            }
            catch (Exception ex) { completion.TrySetException(ex); }
        });
        if (!await completion.Task.WaitAsync(ct)) throw new OperationCanceledException("Разрешение VPN не предоставлено.");
    }

    /// <summary>Передает результат системного диалога ожидающему подключению.</summary>
#pragma warning disable CS0672, CS0618
    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode == VpnPermissionRequest) { permission?.TrySetResult(resultCode == Result.Ok); permission = null; }
    }
#pragma warning restore CS0672, CS0618
    /// <summary>Подключает встроенный шрифт к Android-приложению Avalonia.</summary>
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).WithInterFont();

    /// <summary>Удаляет ссылку на закрытый экран, сохраняя службу и модель приложения для следующего открытия.</summary>
    protected override void OnDestroy()
    {
        if (current == this) current = null;
        base.OnDestroy();
    }
}
