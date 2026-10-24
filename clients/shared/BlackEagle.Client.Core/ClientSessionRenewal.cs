using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;

namespace BlackEagle.Client.Core;

public interface IClientSessionRenewal
{
    /// <summary>Обновляет срок сессии, не меняя состояние VPN и каталог серверов.</summary>
    Task<ClientSession> RenewSessionAsync(CancellationToken ct);
}

internal static class ClientSessionRenewal
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>Последовательно продлевает сессию до истечения и сохраняет новый секретный ответ целиком.</summary>
    internal static async Task<ClientSession> EnsureAsync(HttpClient http, IClientSessionStore store, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var session = await store.LoadAsync(ct) ?? throw new ClientAccessException(HttpStatusCode.Unauthorized);
            var now = DateTimeOffset.UtcNow;
            if (session.RefreshToken is not null && !session.RefreshUpgradePending && session.ExpiresAt > now.AddDays(1)) return session;
            if (session.RefreshToken is null && session.ExpiresAt <= now || session.RefreshExpiresAt <= now)
                throw new ClientAccessException(HttpStatusCode.Unauthorized);
            if (session.RefreshToken is null)
            {
                // Сохраняем секрет до миграции: потерянный ответ не лишает клиента возможности повторить запрос.
                session = session with { RefreshToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant(), RefreshUpgradePending = true };
                await store.SaveAsync(session, ct);
            }
            var upgrading = session.RefreshUpgradePending && session.ExpiresAt > now;
            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/auth/session/refresh");
            if (upgrading) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
            request.Content = JsonContent.Create(new { refreshToken = session.RefreshToken, upgrade = upgrading });
            using var response = await http.SendAsync(request, ct);
            // Поддержка ещё не обновленного сервера, но только пока старая сессия действительна.
            if (response.StatusCode == HttpStatusCode.NotFound && upgrading) return session;
            if (!response.IsSuccessStatusCode) throw new ClientAccessException(response.StatusCode);
            var renewed = await response.Content.ReadFromJsonAsync<ClientSession>(ct) ?? throw new InvalidDataException("Empty renewal response.");
            if (renewed.Account is null || renewed.Account.Id != session.Account.Id || renewed.Account.TelegramId != session.Account.TelegramId ||
                !IsToken(renewed.AccessToken) || !IsToken(renewed.RefreshToken) || renewed.ExpiresAt <= now ||
                renewed.RefreshExpiresAt is not { } refreshUntil || refreshUntil <= now)
                throw new InvalidDataException("Invalid renewal response.");
            // Не перезаписываем профиль, если пользователь успел заменить его или выйти.
            var current = await store.LoadAsync(ct);
            if (current?.AccessToken != session.AccessToken)
                return current ?? throw new ClientAccessException(HttpStatusCode.Unauthorized);
            await store.SaveAsync(renewed, ct);
            return renewed;
        }
        finally { Gate.Release(); }
    }

    /// <summary>Сохраняет импортированный профиль без гонки с фоновым продлением.</summary>
    internal static async Task SaveImportedAsync(IClientSessionStore store, ClientSession session, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try { await store.SaveAsync(session, ct); }
        finally { Gate.Release(); }
    }

    /// <summary>Обновляет сведения аккаунта, не возвращая в хранилище уже замененный токен.</summary>
    internal static async Task<ClientSession> SaveAccountAsync(IClientSessionStore store, ClientSession expected, ClientAccount account, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var current = await store.LoadAsync(ct) ?? throw new ClientAccessException(HttpStatusCode.Unauthorized);
            if (current.Account.Id != expected.Account.Id || account.Id != expected.Account.Id)
                throw new ClientAccessException(HttpStatusCode.Conflict);
            if (current.AccessToken != expected.AccessToken) return current;
            current = current with { Account = account };
            await store.SaveAsync(current, ct);
            return current;
        }
        finally { Gate.Release(); }
    }

    /// <summary>Удаляет при явном выходе только ту сессию, которая была отозвана сервером.</summary>
    internal static async Task ClearIfCurrentAsync(IClientSessionStore store, string token, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try { if ((await store.LoadAsync(ct))?.AccessToken == token) await store.ClearAsync(ct); }
        finally { Gate.Release(); }
    }

    /// <summary>Проверяет формат случайного 256-битного секрета перед сохранением ответа.</summary>
    private static bool IsToken(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
