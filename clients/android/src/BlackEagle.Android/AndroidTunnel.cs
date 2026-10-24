using System.Security.Cryptography;
using Android.Content;
using Android.OS;
using BlackEagle.Client.Core;

namespace BlackEagle.Android;

public sealed class AndroidTunnel : ITunnelPlatform
{
    public static AndroidTunnel Instance { get; } = new();
    private readonly SemaphoreSlim gate = new(1, 1);
    internal TaskCompletionSource<BlackEagleVpnService> Ready { get; private set; } = NewCompletion();
    internal BlackEagleVpnService? Service { get; private set; }
    public event EventHandler<TunnelStatus>? StatusChanged;
    public string State { get; private set; } = "disconnected";
    public long? ConnectedAtMilliseconds { get; private set; }
    public string? ActiveDns { get; private set; }
    public bool ActiveAdblock { get; private set; }

    /// <summary>Запоминает фактически выданный сервером DNS после успешного handshake.</summary>
    internal void PublishDns(string dns, bool adblockEnabled)
    {
        ActiveDns = dns;
        ActiveAdblock = adblockEnabled;
    }

    /// <summary>Создает сигнал готовности VPN-службы с асинхронными продолжениями.</summary>
    private static TaskCompletionSource<BlackEagleVpnService> NewCompletion() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Регистрирует созданную системой VPN-службу.</summary>
    internal void Attach(BlackEagleVpnService service) { Service = service; Ready.TrySetResult(service); }

    /// <summary>Сбрасывает ссылку на уничтоженную службу и уведомляет интерфейс.</summary>
    internal void Detach(BlackEagleVpnService service)
    {
        if (Service != service) return;
        Service = null; Ready = NewCompletion();
        Publish("disconnected", "Отключено");
    }

    /// <summary>Передает интерфейсу подтвержденное состояние туннеля.</summary>
    internal void Publish(string state, string message)
    {
        if (state == "connected" && ConnectedAtMilliseconds is null)
            ConnectedAtMilliseconds = SystemClock.ElapsedRealtime();
        if (state == "disconnected")
        {
            ConnectedAtMilliseconds = null;
            ActiveDns = null;
            ActiveAdblock = false;
        }
        State = state;
        StatusChanged?.Invoke(this, new(state, message));
    }

    /// <summary>Создает ключ устройства и очищает временную копию приватного ключа.</summary>
    public Task<TunnelIdentity> CreateIdentityAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var secret = new byte[32]; var publicKey = new byte[32];
        try
        {
            if (AwgNative.Keys(secret, publicKey) != 0) throw new IOException("Не удалось создать ключ VPN.");
            return Task.FromResult(new TunnelIdentity(Convert.ToBase64String(secret), Convert.ToBase64String(publicKey)));
        }
        finally { CryptographicOperations.ZeroMemory(secret); }
    }

    /// <summary>Запрашивает разрешение Android, запускает службу и ожидает реального handshake.</summary>
    public async Task ConnectAsync(TunnelRequest request, CancellationToken ct)
    {
        TunnelValidation.Validate(request);
        await gate.WaitAsync(ct);
        try
        {
            await MainActivity.RequestVpnPermissionAsync(ct);
            ct.ThrowIfCancellationRequested();
            if (Service is null)
            {
                var context = global::Android.App.Application.Context;
                var intent = new Intent(context, typeof(BlackEagleVpnService));
                if (OperatingSystem.IsAndroidVersionAtLeast(26)) context.StartForegroundService(intent);
                else context.StartService(intent);
            }
            var service = Service ?? await Ready.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            await service.ConnectAsync(request, ct);
        }
        catch
        {
            Service?.CloseTunnel();
            Service?.StopSelf();
            throw;
        }
        finally { gate.Release(); }
    }

    /// <summary>Отключает только VPN данного приложения и останавливает его службу.</summary>
    public async Task DisconnectAsync(CancellationToken ct)
    {
        if (Service is { } service) await service.StopBackgroundConnectionAsync(ct);
        await gate.WaitAsync(ct);
        try { Service?.CloseTunnel(); Service?.StopSelf(); }
        finally { gate.Release(); }
    }
}
