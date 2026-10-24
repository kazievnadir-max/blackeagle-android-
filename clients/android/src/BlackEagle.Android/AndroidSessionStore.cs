using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Android.Content;
using Android.Security.Keystore;
using BlackEagle.Client.Core;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;

namespace BlackEagle.Android;

public sealed class AndroidSessionStore : IClientSessionStore, ITunnelStore
{
    private const string KeyAlias = "blackeagle-client-session";
    private const string PreferenceName = "blackeagle-secure-session";
    private const string SessionKey = "session";
    private readonly ISharedPreferences preferences;

    /// <summary>Открывает приватное хранилище настроек Android.</summary>
    public AndroidSessionStore()
    {
        preferences = global::Android.App.Application.Context!
            .GetSharedPreferences(PreferenceName, FileCreationMode.Private)!;
    }

    /// <summary>Шифрует и сохраняет клиентскую сессию ключом из Android Keystore.</summary>
    public Task SaveAsync(ClientSession session, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var json = JsonSerializer.Serialize(session);
        var encrypted = Encrypt(json);
        if (!preferences.Edit()!.PutString(SessionKey, encrypted)!.Commit())
            throw new IOException("Не удалось сохранить сессию Android.");
        return Task.CompletedTask;
    }

    /// <summary>Расшифровывает ранее сохраненную клиентскую сессию.</summary>
    public Task<ClientSession?> LoadAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var value = preferences.GetString(SessionKey, null);
        if (string.IsNullOrEmpty(value))
            return Task.FromResult<ClientSession?>(null);
        try
        {
            return Task.FromResult(JsonSerializer.Deserialize<ClientSession>(Decrypt(value)));
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException or Java.Lang.Exception)
        {
            preferences.Edit()!.Remove(SessionKey)!.Commit();
            return Task.FromResult<ClientSession?>(null);
        }
    }

    /// <summary>Удаляет локальную клиентскую сессию.</summary>
    public Task ClearAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        preferences.Edit()!.Remove(SessionKey)!.Commit();
        return Task.CompletedTask;
    }

    /// <summary>Загружает зашифрованный ключ устройства и конфигурацию указанного аккаунта.</summary>
    public Task<TunnelIdentity?> LoadTunnelAsync(Guid accountId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var value = preferences.GetString("tunnel-" + accountId.ToString("N"), null);
        return Task.FromResult(value is null ? null : JsonSerializer.Deserialize<TunnelIdentity>(Decrypt(value)));
    }

    /// <summary>Сохраняет ключ и конфигурацию туннеля отдельно для текущего аккаунта.</summary>
    public Task SaveTunnelAsync(Guid accountId, TunnelIdentity value, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!preferences.Edit()!.PutString("tunnel-" + accountId.ToString("N"), Encrypt(JsonSerializer.Serialize(value)))!.Commit())
            throw new IOException("Не удалось сохранить параметры VPN.");
        return Task.CompletedTask;
    }

    /// <summary>Шифрует строку алгоритмом AES-GCM и возвращает сериализованный контейнер.</summary>
    private static string Encrypt(string value)
    {
        using var cipher = Cipher.GetInstance("AES/GCM/NoPadding")!;
        cipher.Init(Javax.Crypto.CipherMode.EncryptMode, GetOrCreateKey());
        var encrypted = cipher.DoFinal(Encoding.UTF8.GetBytes(value))!;
        return JsonSerializer.Serialize(new EncryptedValue(
            Convert.ToBase64String(cipher.GetIV()!), Convert.ToBase64String(encrypted)));
    }

    /// <summary>Расшифровывает сериализованный контейнер алгоритмом AES-GCM.</summary>
    private static string Decrypt(string value)
    {
        var payload = JsonSerializer.Deserialize<EncryptedValue>(value) ?? throw new CryptographicException("Пустой контейнер сессии.");
        var iv = Convert.FromBase64String(payload.Iv);
        var data = Convert.FromBase64String(payload.Data);
        using var cipher = Cipher.GetInstance("AES/GCM/NoPadding")!;
        using var parameters = new GCMParameterSpec(128, iv);
        cipher.Init(Javax.Crypto.CipherMode.DecryptMode, GetOrCreateKey(), parameters);
        return Encoding.UTF8.GetString(cipher.DoFinal(data)!);
    }

    /// <summary>Возвращает существующий ключ Android Keystore либо создает новый.</summary>
    private static Javax.Crypto.ISecretKey GetOrCreateKey()
    {
        using var store = KeyStore.GetInstance("AndroidKeyStore")!;
        store.Load(null);
        if (store.GetEntry(KeyAlias, null) is KeyStore.SecretKeyEntry entry)
            return entry.SecretKey!;

        using var generator = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes, "AndroidKeyStore")!;
        using var specification = new KeyGenParameterSpec.Builder(
                KeyAlias, KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
            .SetBlockModes(KeyProperties.BlockModeGcm)
            .SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone)
            .SetKeySize(256)
            .Build();
        generator.Init(specification);
        return generator.GenerateKey()!;
    }

    private sealed record EncryptedValue(string Iv, string Data);
}
