#if DESKTOP_SHARED
namespace BlackEagle.Client.UI;
#else
namespace BlackEagle.Android;
#endif

internal sealed class ConnectionFeedback
{
    internal const string ConnectionError = "Не удалось подключиться. Проверьте разрешение VPN, подписку и доступность сервера.";
    private bool succeeded;

    /// <summary>Начинает новую попытку, для которой ошибка снова должна быть видимой.</summary>
    public void BeginAttempt() => succeeded = false;

    /// <summary>Запоминает результат попытки после подтверждения состояния VPN-платформой.</summary>
    public void CompleteAttempt(bool connected) => succeeded = connected;

    /// <summary>Убирает устаревшую ошибку подключения после успеха, не скрывая другие сообщения.</summary>
    public string Display(string message) => message == "Пинг измерен по ICMP. «Нет ICMP» не означает, что VPN-сервер недоступен."
        || (succeeded && message == ConnectionError) ? "" : message;
}
