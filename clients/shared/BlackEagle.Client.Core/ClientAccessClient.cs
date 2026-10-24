using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BlackEagle.Client.Core;

public sealed record ClientAccount(Guid Id, long TelegramId, DateTimeOffset? AccessUntil);
public sealed record ClientSession(string AccessToken, DateTimeOffset ExpiresAt, ClientAccount Account,
    string? RefreshToken = null, DateTimeOffset? RefreshExpiresAt = null, bool RefreshUpgradePending = false);
public sealed record ServerInfo(Guid Id, string Name, string CountryCode, string City, string? EndpointHost = null,
    bool AdblockSupported = false);
public sealed record AccessResult(ClientAccount Account, IReadOnlyList<ServerInfo> Servers, bool RequiresActivation);

public interface IClientSessionStore
{
    /// <summary>Сохраняет сессию в защищенном хранилище соответствующей платформы.</summary>
    Task SaveAsync(ClientSession session, CancellationToken ct);
    /// <summary>Читает ранее сохраненную сессию приложения.</summary>
    Task<ClientSession?> LoadAsync(CancellationToken ct);
    /// <summary>Удаляет локальную сессию приложения.</summary>
    Task ClearAsync(CancellationToken ct);
}

public sealed class ClientAccessException(HttpStatusCode status) : Exception("Client access request failed.")
{
    public HttpStatusCode Status { get; } = status;
}

public interface IClientAccessClient
{
    /// <summary>Обменивает ссылку и загружает профиль и серверы.</summary>
    Task<AccessResult> ImportAsync(string link, CancellationToken ct = default);
    /// <summary>Обновляет данные текущей сессии.</summary>
    Task<AccessResult> RefreshAsync(CancellationToken ct = default);
    /// <summary>Завершает текущую сессию.</summary>
    Task LogoutAsync(CancellationToken ct = default);
}

public sealed class ClientAccessClient : IClientAccessClient, IClientSessionRenewal, IDisposable
{
    private readonly HttpClient http;
    private readonly IClientSessionStore store;
    private readonly SemaphoreSlim gate = new(1, 1);

    /// <summary>Настраивает доверенный API независимо от вставляемой ссылки и запрещает HTTP-перенаправления.</summary>
    public ClientAccessClient(Uri apiBase, IClientSessionStore store, HttpMessageHandler? handler = null, bool allowLoopbackDevelopment = false)
    {
        if (!apiBase.IsAbsoluteUri || apiBase.AbsolutePath != "/" || apiBase.UserInfo.Length != 0 || apiBase.Query.Length != 0 ||
            apiBase.Fragment.Length != 0 || (apiBase.Scheme != "https" && !(allowLoopbackDevelopment && apiBase.Scheme == "http" && apiBase.IsLoopback)))
            throw new ArgumentException("A trusted HTTPS API origin is required.");
        this.store = store;
        http = new HttpClient(handler ?? new SocketsHttpHandler { AllowAutoRedirect = false, PooledConnectionLifetime = TimeSpan.FromMinutes(2) })
        { BaseAddress = apiBase, Timeout = TimeSpan.FromSeconds(20) };
    }

    /// <summary>Проверяет ссылку, сохраняет полученную сессию и загружает доступные серверы.</summary>
    public async Task<AccessResult> ImportAsync(string link, CancellationToken ct = default)
    {
        var token = ParseToken(link);
        await gate.WaitAsync(ct);
        try
        {
            using var response = await http.PostAsJsonAsync("v1/auth/client-links/exchange", new { token }, ct);
            if (response.StatusCode == HttpStatusCode.NotFound) throw new ClientAccessException(HttpStatusCode.Unauthorized);
            EnsureSuccess(response);
            var session = await response.Content.ReadFromJsonAsync<ClientSession>(ct) ?? throw new JsonException("Empty session.");
            // Сохраняем до запроса серверов: временный сетевой сбой не теряет уже погашенную ссылку.
            await ClientSessionRenewal.SaveImportedAsync(store, session, ct);
            return await ReadAccessAsync(session, ct);
        }
        finally { gate.Release(); }
    }

    /// <summary>Обновляет профиль и список серверов без повторного использования одноразовой ссылки.</summary>
    public async Task<AccessResult> RefreshAsync(CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try
        {
            var session = await ClientSessionRenewal.EnsureAsync(http, store, ct);
            using var response = await GetAsync("v1/me", session.AccessToken, ct);
            await ClientSessionErrors.CheckAsync(http, response, "v1/me", session.AccessToken, store, ct);
            var account = await response.Content.ReadFromJsonAsync<ClientAccount>(ct) ?? throw new JsonException("Empty account.");
            session = await ClientSessionRenewal.SaveAccountAsync(store, session, account, ct);
            return await ReadAccessAsync(session, ct);
        }
        finally { gate.Release(); }
    }

    /// <summary>Обновляет срок сессии независимо от каталога серверов и состояния VPN.</summary>
    public async Task<ClientSession> RenewSessionAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try { return await ClientSessionRenewal.EnsureAsync(http, store, ct); }
        finally { gate.Release(); }
    }

    /// <summary>Завершает серверную сессию данного приложения и удаляет локальный токен после подтверждения.</summary>
    public async Task LogoutAsync(CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (await store.LoadAsync(ct) is { } session)
            {
                session = await ClientSessionRenewal.EnsureAsync(http, store, ct);
                using var request = new HttpRequestMessage(HttpMethod.Post, "v1/auth/logout");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
                using var response = await http.SendAsync(request, ct);
                try { await ClientSessionErrors.CheckAsync(http, response, "v1/auth/logout", session.AccessToken, store, ct); }
                catch (ClientAccessException ex) when (ex.Status == HttpStatusCode.Unauthorized) { }
                await ClientSessionRenewal.ClearIfCurrentAsync(store, session.AccessToken, ct);
            }
        }
        finally { gate.Release(); }
    }

    /// <summary>Принимает только ссылку доверенного сервиса с каноническим одноразовым секретом во фрагменте.</summary>
    private string ParseToken(string link)
    {
        if (link is null || link.Length > 2048 || !Uri.TryCreate(link.Trim(), UriKind.Absolute, out var uri) ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 ||
            uri.GetLeftPart(UriPartial.Path) != new Uri(http.BaseAddress!, "connect").AbsoluteUri ||
            !uri.Fragment.StartsWith("#token=", StringComparison.Ordinal))
            throw new ArgumentException("Invalid BlackEagle login link.");
        var token = uri.Fragment[7..];
        if (token.Length != 64 || token.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new ArgumentException("Invalid BlackEagle login link.");
        return token;
    }

    /// <summary>Загружает серверы и отличает отсутствие подписки от ошибки входа.</summary>
    private async Task<AccessResult> ReadAccessAsync(ClientSession session, CancellationToken ct)
    {
        using var response = await GetAsync("v1/servers", session.AccessToken, ct);
        if (response.StatusCode == HttpStatusCode.Forbidden)
            return new(session.Account, Array.Empty<ServerInfo>(), true);
        await ClientSessionErrors.CheckAsync(http, response, "v1/servers", session.AccessToken, store, ct);
        var servers = await response.Content.ReadFromJsonAsync<List<ServerInfo>>(ct) ?? throw new JsonException("Empty server list.");
        return new(session.Account, servers, false);
    }

    /// <summary>Отправляет пользовательский токен только в заголовке запроса доверенному API.</summary>
    private async Task<HttpResponseMessage> GetAsync(string path, string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await http.SendAsync(request, ct);
    }

    /// <summary>Преобразует ошибку API без включения секретов или тела ответа в исключение.</summary>
    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode) throw new ClientAccessException(response.StatusCode);
    }

    /// <summary>Освобождает HTTP-транспорт и локальную синхронизацию клиента.</summary>
    public void Dispose() { http.Dispose(); gate.Dispose(); }
}
