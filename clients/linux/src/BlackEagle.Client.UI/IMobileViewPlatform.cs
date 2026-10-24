namespace BlackEagle.Client.UI;

/// <summary>Предоставляет мобильной компоновке только необходимые платформенные функции.</summary>
public interface IMobileViewPlatform
{
    bool IsConnected { get; }
    TimeSpan? ConnectedDuration { get; }
    string AppVersion { get; }
    bool SupportsAutostart { get; }
    bool IsAutostartEnabled { get; }

    /// <summary>Включает или отключает запуск приложения при входе пользователя в систему.</summary>
    bool SetAutostartEnabled(bool enabled);

    /// <summary>Открывает официальный Telegram-бот средствами текущей платформы.</summary>
    void OpenTelegram();
}
