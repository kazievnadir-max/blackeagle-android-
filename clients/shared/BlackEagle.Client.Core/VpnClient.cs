using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BlackEagle.NodeProtocol;

namespace BlackEagle.Client.Core;

public sealed record TunnelIdentity(string PrivateKey, string PublicKey, Guid? DeviceId = null,
    Guid? ServerId = null, VpnConfiguration? Configuration = null);
public sealed record TunnelRequest(string PrivateKey, VpnConfiguration Configuration);
public sealed record TunnelStatus(string State, string Message);

public interface ITunnelStore
{
    /// <summary>Загружает ключ и конфигурацию только указанного аккаунта.</summary>
    Task<TunnelIdentity?> LoadTunnelAsync(Guid accountId, CancellationToken ct);
    /// <summary>Сохраняет ключ и конфигурацию в защищенном хранилище платформы.</summary>
    Task SaveTunnelAsync(Guid accountId, TunnelIdentity value, CancellationToken ct);
}

public interface ITunnelPlatform
{
    event EventHandler<TunnelStatus>? StatusChanged;
    /// <summary>Создает ключ устройства средствами неизмененного AWG.</summary>
    Task<TunnelIdentity> CreateIdentityAsync(CancellationToken ct);
    /// <summary>Поднимает туннель после системного подтверждения и ожидает handshake.</summary>
    Task ConnectAsync(TunnelRequest request, CancellationToken ct);
    /// <summary>Закрывает только принадлежащий приложению туннель.</summary>
    Task DisconnectAsync(CancellationToken ct);
}

public interface IVpnClient
{
    event EventHandler<TunnelStatus>? StatusChanged;
    /// <summary>Получает свежие права и подключает выбранный сервер.</summary>
    Task ConnectAsync(Guid serverId, CancellationToken ct);
    /// <summary>Отключает туннель, сохраняя конфигурацию для следующего запуска.</summary>
    Task DisconnectAsync(CancellationToken ct);
    /// <summary>Возвращает последний выбранный сервер текущего аккаунта.</summary>
    Task<Guid?> LastServerAsync(CancellationToken ct);
}

public sealed class VpnClient : IVpnClient, IDisposable
{
    private readonly HttpClient http;
    private readonly IClientSessionStore sessions;
    private readonly ITunnelStore store;
    private readonly ITunnelPlatform platform;
    private readonly string devicePlatform;
    private readonly Func<bool> adblockEnabled;
    private readonly SemaphoreSlim gate = new(1);
    public event EventHandler<TunnelStatus>? StatusChanged;

    /// <summary>Связывает переносимый API туннеля с защищенным хранилищем и платформенным адаптером.</summary>
    public VpnClient(Uri origin, IClientSessionStore sessions, ITunnelStore store, ITunnelPlatform platform,
        HttpMessageHandler? handler = null, bool allowLocal = false, string devicePlatform = "linux", Func<bool>? adblockEnabled = null)
    {
        // Используем ту же проверку origin, что и вход по ссылке.
        using var validator = new ClientAccessClient(origin, sessions, allowLoopbackDevelopment: allowLocal);
        this.sessions = sessions; this.store = store; this.platform = platform;
        this.devicePlatform = devicePlatform;
        this.adblockEnabled = adblockEnabled ?? (() => false);
        http = new(handler ?? new SocketsHttpHandler { AllowAutoRedirect = false })
        { BaseAddress = origin, Timeout = TimeSpan.FromSeconds(20) };
        platform.StatusChanged += ForwardStatus;
    }

    /// <summary>Передает состояние ОС без зависимости от UI-фреймворка.</summary>
    private void ForwardStatus(object? sender, TunnelStatus status) => StatusChanged?.Invoke(this, status);

    /// <summary>Регистрирует стабильный ключ устройства и каждый раз запрашивает актуальную конфигурацию.</summary>
    public async Task ConnectAsync(Guid serverId, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            var session = await ClientSessionRenewal.EnsureAsync(http, sessions, ct);
            var identity = await store.LoadTunnelAsync(session.Account.Id, ct) ?? await platform.CreateIdentityAsync(ct);
            // Сохраняем ключ до регистрации: повтор после потерянного HTTP-ответа найдет то же устройство.
            await store.SaveTunnelAsync(session.Account.Id, identity, ct);
            var devices = await SendAsync<List<DeviceRecord>>(HttpMethod.Get, "v1/devices", session.AccessToken, null, ct);
            var device = devices.FirstOrDefault(d => d.PublicKey == identity.PublicKey) ??
                await SendAsync<DeviceRecord>(HttpMethod.Post, "v1/devices", session.AccessToken,
                    new { name = devicePlatform switch { "android" => "Android", "ios" => "iOS", _ => "Desktop" }, platform = devicePlatform, publicKey = identity.PublicKey }, ct);
            if (identity.DeviceId != device.Id)
            {
                identity = identity with { DeviceId = device.Id, ServerId = null, Configuration = null };
                await store.SaveTunnelAsync(session.Account.Id, identity, ct);
            }
            var useAdblock = adblockEnabled();
            var path = $"v1/devices/{identity.DeviceId}/nodes/{serverId}/configuration" + (useAdblock ? "?adblock=true" : "");
            var config = await SendAsync<VpnConfiguration>(HttpMethod.Post, path, session.AccessToken, null, ct);
            if (config.NodeId != serverId || config.DeviceId != identity.DeviceId) throw new InvalidDataException();
            if (useAdblock && (config.AdblockEnabled != true || config.Subnet is null ||
                config.Dns != NodeValidation.Gateway(config.Subnet).Split('/')[0]))
                throw new InvalidDataException("Сервер не подтвердил фильтрующий DNS собственной VPN-ноды.");
            TunnelValidation.Validate(new(identity.PrivateKey, config));
            await store.SaveTunnelAsync(session.Account.Id, identity with { ServerId = serverId, Configuration = config }, ct);
            await platform.ConnectAsync(new(identity.PrivateKey, config), ct);
        }
        finally { gate.Release(); }
    }

    /// <summary>Отправляет типизированный запрос без передачи токена в URL или журналы.</summary>
    private async Task<T> SendAsync<T>(HttpMethod method, string path, string token, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await http.SendAsync(request, ct);
        await ClientSessionErrors.CheckAsync(http, response, path, token, sessions, ct);
        return await response.Content.ReadFromJsonAsync<T>(ct) ?? throw new InvalidDataException();
    }

    /// <summary>Закрывает платформенное соединение независимо от доступности API.</summary>
    public Task DisconnectAsync(CancellationToken ct) => platform.DisconnectAsync(ct);

    /// <summary>Восстанавливает выбор сервера без подключения по устаревшим правам.</summary>
    public async Task<Guid?> LastServerAsync(CancellationToken ct)
    {
        var session = await sessions.LoadAsync(ct);
        return session is null ? null : (await store.LoadTunnelAsync(session.Account.Id, ct))?.ServerId;
    }

    /// <summary>Освобождает HTTP-клиент и подписку на события платформы.</summary>
    public void Dispose() { platform.StatusChanged -= ForwardStatus; http.Dispose(); gate.Dispose(); }

    private sealed record DeviceRecord(Guid Id, string PublicKey);
}

public static class TunnelValidation
{
    /// <summary>Отклоняет произвольные маршруты, команды, адреса и неподдерживаемые профили до повышения прав.</summary>
    public static void Validate(TunnelRequest request)
    {
        var c = request.Configuration ?? throw new ArgumentException("Missing configuration.");
        NodeValidation.KeyHex(request.PrivateKey);
        if (c.NodeId == Guid.Empty || c.DeviceId == Guid.Empty || c.Mtu != 1280 ||
            c.AllowedIPs != "0.0.0.0/0, ::/0" || c.PersistentKeepalive != 25 ||
            c.Endpoint is null || c.Endpoint.Length > 260) throw new ArgumentException("Unsupported configuration.");
        var parts = c.Endpoint.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[1], out var port) || parts[1] != port.ToString(System.Globalization.CultureInfo.InvariantCulture)) throw new ArgumentException("Invalid endpoint.");
        if (c.Address is null || !c.Address.EndsWith("/32") ||
            !IPAddress.TryParse(c.Address[..^3], out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            throw new ArgumentException("Invalid address.");
        var b = ip.GetAddressBytes();
        var subnet = c.Subnet ?? $"{b[0]}.{b[1]}.{b[2]}.0/24";
        NodeValidation.Validate(new("Desktop", "DE", "Desktop", parts[0], port, c.ServerPublicKey, subnet, c.Dns, c.Profile));
        NodeValidation.ValidateAddress(subnet, c.Address);
    }
}
