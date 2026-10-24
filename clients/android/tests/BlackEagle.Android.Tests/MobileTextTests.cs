using Xunit;

namespace BlackEagle.Android;

public sealed class MobileTextTests
{
    /// <summary>Проверяет английские состояния VPN, подписки, пинга и ошибки без изменения Linux-ресурсов.</summary>
    [Theory]
    [InlineData("Подключено", "Connected")]
    [InlineData("Отключено", "Disconnected")]
    [InlineData("Восстановление соединения…", "Reconnecting…")]
    [InlineData("Подписка активна", "Subscription active")]
    [InlineData("Сессия истекла", "Session expired")]
    [InlineData("Нет ICMP", "No ICMP")]
    [InlineData("32 мс", "32 ms")]
    [InlineData("Доступ до 15.09.2026 10:00 (местное время)", "Access until 15.09.2026 10:00 (local time)")]
    [InlineData("Telegram ID: 12345", "Telegram ID: 12345")]
    [InlineData("Слишком много запросов. Подождите минуту.", "Too many requests. Please wait a minute.")]
    public void TranslatesMessages(string source, string expected) => Assert.Equal(expected, MobileText.English(source));
}
