using System.Globalization;

namespace BlackEagle.Client.UI;

public static class MobilePreferences
{
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "blackeagle-vpn");
    private static readonly string SettingsPath = Path.Combine(DirectoryPath, "ui-settings");
    private static readonly Dictionary<string, string> Values = Load();
    public static string Language => Values.GetValueOrDefault("language", CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? "ru" : "en");
    public static bool IsEnglish => Language == "en";
    public static string Theme => Values.GetValueOrDefault("theme", "system");
    public static bool AdBlockingEnabled => Values.GetValueOrDefault("adblock-enabled") == "true";

    /// <summary>Сохраняет несекретную настройку desktop-интерфейса.</summary>
    public static void Save(string key, string value) => _ = TrySave(key, value);

    /// <summary>Сохраняет выбор фильтрующего DNS и сообщает об ошибке записи интерфейсу.</summary>
    public static bool SaveAdBlocking(bool enabled) => TrySave("adblock-enabled", enabled ? "true" : "false");

    /// <summary>Атомарно записывает настройку, не меняя состояние в памяти при ошибке.</summary>
    private static bool TrySave(string key, string value)
    {
        try
        {
            var updated = new Dictionary<string, string>(Values) { [key] = value };
            Directory.CreateDirectory(DirectoryPath);
            var temporary = SettingsPath + ".tmp";
            File.WriteAllLines(temporary, updated.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));
            File.Move(temporary, SettingsPath, true);
            Values[key] = value;
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    /// <summary>Загружает избранные идентификаторы серверов, пропуская поврежденные значения.</summary>
    public static HashSet<Guid> Favorites() => Values.GetValueOrDefault("favorites", "").Split(',')
        .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).ToHashSet();

    /// <summary>Сохраняет только идентификаторы избранных серверов.</summary>
    public static void SaveFavorites(IEnumerable<Guid> values) => Save("favorites", string.Join(',', values));

    /// <summary>Читает допустимые пары ключ-значение из пользовательского файла.</summary>
    private static Dictionary<string, string> Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return [];
            return File.ReadAllLines(SettingsPath).Select(line => line.Split('=', 2))
                .Where(parts => parts.Length == 2 && parts[0] is "language" or "theme" or "favorites" or "adblock-enabled")
                .ToDictionary(parts => parts[0], parts => parts[1]);
        }
        catch (IOException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
    }
}
