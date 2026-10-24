using Android.Content;
using System.Globalization;

namespace BlackEagle.Android;

internal static class MobilePreferences
{
    private static readonly ISharedPreferences Store = global::Android.App.Application.Context
        .GetSharedPreferences("blackeagle-mobile-ui", FileCreationMode.Private)!;
    public static string Language => Store.GetString("language", CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? "ru" : "en")!;
    public static bool IsEnglish => Language == "en";
    public static string Theme => Store.GetString("theme", "system")!;
    public static string SelectedServer => Store.GetString("selected-server", "")!;
    public static bool AdBlockingEnabled => Read("adblock-enabled", "false") == "true";

    /// <summary>Надежно сохраняет выбор DNS до следующего запроса конфигурации VPN.</summary>
    public static bool SaveAdBlocking(bool enabled) => Store.Edit()!
        .PutString("adblock-enabled", enabled ? "true" : "false")!.Commit();
    /// <summary>Читает несекретную настройку с безопасным значением по умолчанию.</summary>
    public static string Read(string key, string fallback) => Store.GetString(key, fallback)!;

    /// <summary>Сохраняет несекретную настройку интерфейса без доступа к хранилищу сессии.</summary>
    public static void Save(string key, string value) => Store.Edit()!.PutString(key, value)!.Apply();

    /// <summary>Загружает избранные идентификаторы серверов, пропуская поврежденные значения.</summary>
    public static HashSet<Guid> Favorites() => (Store.GetString("favorites", "") ?? "").Split(',')
        .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).ToHashSet();

    /// <summary>Сохраняет только идентификаторы избранных серверов.</summary>
    public static void SaveFavorites(IEnumerable<Guid> values) => Save("favorites", string.Join(',', values));
}
