using Android.App;
using Android.Content;
using Android.OS;
using Android.Service.QuickSettings;
using BlackEagle.Client.Core;

namespace BlackEagle.Android;

[Service(Name = "com.blackeaglevpn.client.VpnTileService")]
public sealed class VpnTileService : TileService
{
    private bool listening, busy;

    /// <summary>Обновляет плитку при открытии шторки и подписывается на реальное состояние AWG.</summary>
    public override void OnStartListening()
    {
        base.OnStartListening();
        if (!listening) AndroidTunnel.Instance.StatusChanged += TunnelChanged;
        listening = true; UpdateTile();
    }

    /// <summary>Убирает подписку, когда Android больше не отображает плитку.</summary>
    public override void OnStopListening()
    {
        AndroidTunnel.Instance.StatusChanged -= TunnelChanged;
        listening = false; base.OnStopListening();
    }

    /// <summary>Освобождает подписку даже при завершении службы без остановки прослушивания.</summary>
    public override void OnDestroy()
    {
        AndroidTunnel.Instance.StatusChanged -= TunnelChanged;
        base.OnDestroy();
    }

    /// <summary>Переносит изменение состояния VPN на главный поток Android.</summary>
    private void TunnelChanged(object? sender, TunnelStatus status)
    {
        using var handler = new Handler(Looper.MainLooper!);
        handler.Post(() => { if (listening) UpdateTile(); });
    }

    /// <summary>Показывает активную плитку только для установленного или восстанавливаемого туннеля.</summary>
    private void UpdateTile()
    {
        var tile = QsTile;
        if (tile is null) return;
        var active = AndroidTunnel.Instance.State is "connected" or "reconnecting" or "connecting";
        tile.State = busy ? TileState.Unavailable : active ? TileState.Active : TileState.Inactive;
        tile.Label = "Black Eagle VPN";
        tile.Subtitle = MobilePreferences.IsEnglish ? (active ? "Connected" : "Disconnected") : (active ? "Подключено" : "Отключено");
        tile.Icon = global::Android.Graphics.Drawables.Icon.CreateWithResource(this, Resource.Drawable.eagle_head_black);
        tile.UpdateTile();
    }

    /// <summary>Требует разблокировать телефон перед изменением маршрутизации VPN.</summary>
    public override void OnClick()
    {
        base.OnClick();
        if (IsLocked) UnlockAndRun(new Java.Lang.Runnable(() => _ = ToggleAsync()));
        else _ = ToggleAsync();
    }

    /// <summary>Переключает VPN в службе; открывает экран только для первого системного разрешения.</summary>
    private async Task ToggleAsync()
    {
        if (busy) return;
        busy = true; UpdateTile();
        try
        {
            if (AndroidTunnel.Instance.State is "connected" or "reconnecting" or "connecting")
                await AndroidTunnel.Instance.DisconnectAsync(CancellationToken.None);
            else if (global::Android.Net.VpnService.Prepare(this) is null)
                StartForegroundService(new Intent(this, typeof(BlackEagleVpnService)).SetAction(BlackEagleVpnService.ConnectAction));
            else
            {
                TileRequest.Set();
                var intent = new Intent(this, typeof(MainActivity));
                intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);
                if (OperatingSystem.IsAndroidVersionAtLeast(34))
                {
                    using var pending = PendingIntent.GetActivity(this, 21, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
                    StartActivityAndCollapse(pending);
                }
                else
                {
#pragma warning disable CS0618
                    StartActivityAndCollapse(intent);
#pragma warning restore CS0618
                }
            }
        }
        catch (Exception)
        {
            TileRequest.Consume();
            global::Android.Widget.Toast.MakeText(this,
                MobilePreferences.IsEnglish ? "Open Black Eagle VPN to manage the connection." : "Откройте Black Eagle VPN для управления подключением.",
                global::Android.Widget.ToastLength.Long)!.Show();
        }
        finally { busy = false; if (listening) UpdateTile(); }
    }
}
