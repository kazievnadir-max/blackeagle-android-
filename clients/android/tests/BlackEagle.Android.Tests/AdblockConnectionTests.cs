using System.Net;
using System.Net.Http.Json;
using BlackEagle.Client.Core;
using BlackEagle.NodeProtocol;
using Xunit;

namespace BlackEagle.Android.Tests;

[Collection("VPN client integration")]
public sealed class AdblockConnectionTests
{
    private static readonly string Key = Convert.ToBase64String(Enumerable.Repeat((byte)7, 32).ToArray());

    /// <summary>Проверяет, что режим фильтрации выбирается сервером, а не сторонним DNS в приложении.</summary>
    [Theory]
    [InlineData(false, "10.66.0.53")]
    [InlineData(true, "10.66.0.1")]
    public async Task RequestsOnlyServerSelectedDns(bool enabled, string dns)
    {
        var account = Guid.NewGuid(); var server = Guid.NewGuid(); var device = Guid.NewGuid();
        var store = new Store(account, device);
        var platform = new Platform();
        Uri? configurationUrl = null;
        using var handler = new Handler(request =>
        {
            if (request.Method == HttpMethod.Get) return Json(new[] { new { id = device, publicKey = Key } });
            configurationUrl = request.RequestUri;
            return Json(Configuration(server, device, dns, enabled));
        });
        using var client = new VpnClient(new Uri("https://api.example.test/"), store, store, platform, handler,
            devicePlatform: "android", adblockEnabled: () => enabled);
        await client.ConnectAsync(server, default);
        Assert.Equal(enabled ? "?adblock=true" : "", configurationUrl!.Query);
        Assert.Equal(dns, platform.ConnectedDns);
    }

    /// <summary>Запрещает включать VPN с обычным или чужим DNS при запросе фильтрации.</summary>
    [Theory]
    [InlineData("10.66.0.1", false)]
    [InlineData("192.0.2.53", true)]
    public async Task RejectsUnconfirmedOrForeignFilteringDns(string dns, bool confirmed)
    {
        var account = Guid.NewGuid(); var server = Guid.NewGuid(); var device = Guid.NewGuid();
        var store = new Store(account, device);
        var platform = new Platform();
        using var handler = new Handler(request => request.Method == HttpMethod.Get
            ? Json(new[] { new { id = device, publicKey = Key } })
            : Json(Configuration(server, device, dns, confirmed)));
        using var client = new VpnClient(new Uri("https://api.example.test/"), store, store, platform, handler,
            devicePlatform: "android", adblockEnabled: () => true);
        await Assert.ThrowsAsync<InvalidDataException>(() => client.ConnectAsync(server, default));
        Assert.Null(platform.ConnectedDns);
    }

    /// <summary>Создает тестовую конфигурацию с адресом DNS, возвращенным API.</summary>
    private static VpnConfiguration Configuration(Guid server, Guid device, string dns, bool confirmed) =>
        new(server, device, "10.66.0.2/32", dns, "vpn.example.test:51821", Key,
            new(4, 40, 1200, 64, 32, 100001, 200002, 300003, 400004), Subnet: "10.66.0.0/16",
            AdblockEnabled: confirmed);

    /// <summary>Упаковывает ответ имитатора API в JSON.</summary>
    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        /// <summary>Перехватывает HTTP-запрос без обращения к сети.</summary>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(send(request));
    }

    private sealed class Store(Guid account, Guid device) : IClientSessionStore, ITunnelStore
    {
        private TunnelIdentity? tunnel;
        /// <summary>Сохраняет тестовую сессию без изменения её владельца.</summary>
        public Task SaveAsync(ClientSession session, CancellationToken ct) => Task.CompletedTask;
        /// <summary>Возвращает действующую тестовую сессию.</summary>
        public Task<ClientSession?> LoadAsync(CancellationToken ct) => Task.FromResult<ClientSession?>(
            new("test-token", DateTimeOffset.UtcNow.AddDays(7),
                new ClientAccount(account, 123, DateTimeOffset.UtcNow.AddDays(7)),
                new string('a', 64), DateTimeOffset.UtcNow.AddDays(90)));
        /// <summary>Не удаляет тестовую сессию при проверке DNS.</summary>
        public Task ClearAsync(CancellationToken ct) => Task.CompletedTask;
        /// <summary>Загружает ключ устройства из тестового хранилища.</summary>
        public Task<TunnelIdentity?> LoadTunnelAsync(Guid accountId, CancellationToken ct) =>
            Task.FromResult<TunnelIdentity?>(tunnel ?? new TunnelIdentity(Key, Key, device));
        /// <summary>Сохраняет конфигурацию только в памяти теста.</summary>
        public Task SaveTunnelAsync(Guid accountId, TunnelIdentity value, CancellationToken ct)
        { tunnel = value; return Task.CompletedTask; }
    }

    private sealed class Platform : ITunnelPlatform
    {
        public event EventHandler<TunnelStatus>? StatusChanged { add { } remove { } }
        public string? ConnectedDns { get; private set; }
        /// <summary>Возвращает тестовый ключ устройства.</summary>
        public Task<TunnelIdentity> CreateIdentityAsync(CancellationToken ct) => Task.FromResult(new TunnelIdentity(Key, Key));
        /// <summary>Запоминает DNS, который получил бы VPN-туннель.</summary>
        public Task ConnectAsync(TunnelRequest request, CancellationToken ct)
        { ConnectedDns = request.Configuration.Dns; return Task.CompletedTask; }
        /// <summary>Имитирует отключение тестового туннеля.</summary>
        public Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
