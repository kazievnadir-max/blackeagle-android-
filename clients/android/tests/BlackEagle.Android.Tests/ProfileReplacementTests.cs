using System.Net;
using System.Net.Http.Json;
using BlackEagle.Client.Core;
using BlackEagle.Client.UI;
using Xunit;

namespace BlackEagle.Android;

[Collection("VPN client integration")]
public sealed class ProfileReplacementTests
{
    /// <summary>Воспроизводит отозванный профиль с зависшим восстановлением и проверяет замену без очистки хранилища.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task ReplacesRevokedProfile(bool validLink, bool disconnectFails)
    {
        var account = new ClientAccount(Guid.NewGuid(), 123, DateTimeOffset.UtcNow.AddDays(1));
        var store = new Store { Value = new("revoked", DateTimeOffset.UtcNow.AddDays(1), account) };
        var vpn = new Vpn { DisconnectFails = disconnectFails };
        var exchanges = 0;
        using var handler = new Handler(async request =>
        {
            if (request.RequestUri!.AbsolutePath == "/v1/auth/client-links/exchange")
            {
                exchanges++;
                Assert.False(vpn.Active);
                Assert.Null(request.Headers.Authorization);
                Assert.Equal(0, store.Clears);
                return validLink ? Json(new ClientSession("replacement", DateTimeOffset.UtcNow.AddDays(1), account))
                    : new(HttpStatusCode.Unauthorized);
            }
            if (request.RequestUri.AbsolutePath == "/v1/me") return Json(account);
            if (request.Headers.Authorization?.Parameter == "revoked") return new(HttpStatusCode.Forbidden);
            await Task.CompletedTask;
            return Json(new[] { new ServerInfo(Guid.NewGuid(), "Test", "DE", "Test", "192.0.2.1") });
        });
        using var api = new ClientAccessClient(new Uri("https://api.example.test/"), store, handler);
        using var model = new MainViewModel(api, store, TimeProvider.System, "https://api.example.test/", vpn: vpn);
        await model.InitializeAsync();
        vpn.Reconnecting();
        model.IsAdding = true;
        model.Link = "https://api.example.test/connect#token=" + new string('a', 64);
        Assert.False(model.CanImport);
        Assert.False(model.CanSelectServer);
        if (disconnectFails)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => ProfileReplacement.ImportAsync(model));
            Assert.Equal(0, exchanges);
            Assert.Equal(0, store.Clears);
            Assert.Equal("revoked", store.Value!.AccessToken);
            Assert.True(model.IsTunnelActive);
            Assert.NotEmpty(model.Link);
            return;
        }
        await ProfileReplacement.ImportAsync(model);
        Assert.Equal(1, exchanges);
        Assert.Equal(0, store.Clears);
        Assert.False(model.IsTunnelActive);
        Assert.Equal(validLink ? "replacement" : "revoked", store.Value!.AccessToken);
        Assert.Equal(!validLink, model.IsAdding);
        if (validLink) Assert.True(model.HasServers);
    }

    /// <summary>Создает ответ API без реальных токенов или сетевых соединений.</summary>
    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        /// <summary>Передает запрос проверяемому имитатору API.</summary>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request);
    }

    private sealed class Store : IClientSessionStore
    {
        public ClientSession? Value;
        public int Clears;
        /// <summary>Сохраняет тестовую сессию.</summary>
        public Task SaveAsync(ClientSession session, CancellationToken ct) { Value = session; return Task.CompletedTask; }
        /// <summary>Возвращает тестовую сессию.</summary>
        public Task<ClientSession?> LoadAsync(CancellationToken ct) => Task.FromResult(Value);
        /// <summary>Учитывает нежелательную очистку сессии при замене.</summary>
        public Task ClearAsync(CancellationToken ct) { Clears++; Value = null; return Task.CompletedTask; }
    }

    private sealed class Vpn : IVpnClient
    {
        public bool Active;
        public bool DisconnectFails;
        public event EventHandler<TunnelStatus>? StatusChanged;
        /// <summary>Имитирует туннель, который восстанавливается после отзыва доступа на сервере.</summary>
        public void Reconnecting() { Active = true; StatusChanged?.Invoke(this, new("reconnecting", "Восстановление соединения…")); }
        /// <summary>Не подключает реальные серверы в тесте.</summary>
        public Task ConnectAsync(Guid serverId, CancellationToken ct) => Task.CompletedTask;
        /// <summary>Подтверждает отключение старого туннеля до импорта новой ссылки.</summary>
        public Task DisconnectAsync(CancellationToken ct)
        {
            if (DisconnectFails) throw new IOException("Test disconnect failure");
            Active = false; StatusChanged?.Invoke(this, new("disconnected", "Отключено")); return Task.CompletedTask;
        }
        /// <summary>Возвращает отсутствие сохраненного сервера.</summary>
        public Task<Guid?> LastServerAsync(CancellationToken ct) => Task.FromResult<Guid?>(null);
    }
}
