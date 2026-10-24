#if DESKTOP_SHARED
namespace BlackEagle.Client.UI;
#else
namespace BlackEagle.Android;
#endif

internal static class MobileText
{
    private static readonly Dictionary<string, string> Messages = new()
    {
        ["Подключиться"] = "Connect", ["Отключиться"] = "Disconnect", ["Подключено"] = "Connected",
        ["Отключено"] = "Disconnected", ["Не подключено"] = "Not connected",
        ["Подключение… Подтвердите системный запрос."] = "Connecting… Confirm the Android VPN request.",
        ["Восстановление соединения…"] = "Reconnecting…", ["Нет ICMP"] = "No ICMP",
        ["Войдите по ссылке из Telegram"] = "Sign in with a Telegram link", ["Нет данных"] = "No subscription",
        ["Сессия истекла"] = "Session expired", ["Доступ истёк"] = "Access expired",
        ["Требуется обновление"] = "Refresh required", ["Подписка активна"] = "Subscription active",
        ["Подписка или тестовый период на 3 дня активируются в боте."] = "Activate a subscription or a 3-day trial in the bot.",
        ["После входа здесь появятся ваши серверы."] = "Sign in to see your servers here.",
        ["Не удалось получить актуальный список. Нажмите «Обновить»."] = "Could not load the latest server list. Tap Refresh.",
        ["Для просмотра серверов продлите доступ в Telegram."] = "Renew access in Telegram to see your servers.",
        ["Доступных серверов пока нет. Попробуйте обновить список позже."] = "No servers available. Try refreshing later.",
        ["Нажмите +, чтобы добавить подписку по ссылке из Telegram."] = "Tap + to add a profile using your Telegram link.",
        ["Не удалось открыть защищенное хранилище сессии."] = "Could not open secure session storage.",
        ["Не удалось подключиться. Проверьте разрешение VPN, подписку и доступность сервера."] = "Connection failed. Check VPN permission, your subscription and server availability.",
        ["Не удалось подтвердить отключение. Проверьте состояние helper."] = "Could not confirm disconnection. Check the VPN status in Android settings.",
        ["Пинг измерен по ICMP. «Нет ICMP» не означает, что VPN-сервер недоступен."] = "Latency measured using ICMP. No ICMP does not mean the VPN server is offline.",
        ["Не удалось измерить ICMP-пинг."] = "Could not measure ICMP latency.",
        ["Вход выполнен. Список серверов обновлён."] = "Signed in. Server list updated.",
        ["Данные обновлены."] = "Up to date.",
        ["Вы вышли. Для входа получите новую ссылку в Telegram."] = "Signed out. Get a new link in Telegram to sign in.",
        ["Обращаемся к сервису…"] = "Contacting the service…",
        ["Доступ закончился. Продлите подписку в Telegram."] = "Access expired. Renew your subscription in Telegram.",
        ["Ссылка не подходит этому сервису. Скопируйте полную ссылку из Telegram."] = "Invalid service link. Copy the complete link from Telegram.",
        ["Ссылка или сессия истекла либо уже использована. Получите новую ссылку в боте."] = "Link or session expired or already used. Get a new link from the bot.",
        ["Нужна действующая подписка или активированный тест на 3 дня."] = "An active subscription or 3-day trial is required.",
        ["Слишком много запросов. Подождите минуту."] = "Too many requests. Please wait a minute.",
        ["Сервис недоступен. Попробуйте «Обновить». Если сессия не получена — запросите новую ссылку."] = "Service unavailable. Try Refresh. If not signed in, request a new link.",
        ["Связка ключей недоступна. Проверьте Secret Service."] = "Secure storage is unavailable. Try reopening the app."
    };

    /// <summary>Переводит сообщения общей модели, включая динамический срок подписки и пинг.</summary>
    public static string English(string value)
    {
        if (Messages.TryGetValue(value, out var translated)) return translated;
        if (value.StartsWith("Доступ до ", StringComparison.Ordinal))
            return value.Replace("Доступ до ", "Access until ", StringComparison.Ordinal).Replace("(местное время)", "(local time)", StringComparison.Ordinal);
        if (value.EndsWith(" мс", StringComparison.Ordinal)) return value[..^3] + " ms";
        return value;
    }
}
