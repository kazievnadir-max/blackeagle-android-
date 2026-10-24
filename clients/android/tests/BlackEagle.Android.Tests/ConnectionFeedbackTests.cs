using Xunit;

namespace BlackEagle.Android;

public sealed class ConnectionFeedbackTests
{
    /// <summary>Скрывает информационный результат пинга, сохраняя сообщение об ошибке измерения.</summary>
    [Fact]
    public void SuccessfulPingIsSilent()
    {
        var feedback = new ConnectionFeedback();
        const string success = "Пинг измерен по ICMP. «Нет ICMP» не означает, что VPN-сервер недоступен.";
        Assert.Equal("", feedback.Display(success));
        Assert.Equal("", MobileText.English(feedback.Display(success)));
        const string failure = "Не удалось измерить ICMP-пинг.";
        Assert.Equal(failure, feedback.Display(failure));
    }

    /// <summary>Проверяет однократную обработку плитки при повторных событиях запуска экрана.</summary>
    [Fact]
    public void TileRequestIsConsumedOnce()
    {
        TileRequest.Consume();
        Assert.False(TileRequest.Consume());
        TileRequest.Set(); TileRequest.Set();
        Assert.True(TileRequest.Consume());
        Assert.False(TileRequest.Consume());
    }

    /// <summary>Проверяет исчезновение ошибки после успеха и повторное появление при следующем отказе.</summary>
    [Fact]
    public void FailureSuccessFailure()
    {
        var feedback = new ConnectionFeedback();
        feedback.BeginAttempt(); feedback.CompleteAttempt(false);
        Assert.Equal(ConnectionFeedback.ConnectionError, feedback.Display(ConnectionFeedback.ConnectionError));
        feedback.BeginAttempt(); feedback.CompleteAttempt(true);
        Assert.Equal("", feedback.Display(ConnectionFeedback.ConnectionError));
        Assert.Equal("", MobileText.English(feedback.Display(ConnectionFeedback.ConnectionError)));
        feedback.BeginAttempt(); feedback.CompleteAttempt(false);
        Assert.Equal(ConnectionFeedback.ConnectionError, feedback.Display(ConnectionFeedback.ConnectionError));
    }

    /// <summary>Не скрывает ошибки подписки и другие независимые сообщения после успешного подключения.</summary>
    [Fact]
    public void PreservesOtherMessages()
    {
        var feedback = new ConnectionFeedback(); feedback.CompleteAttempt(true);
        const string message = "Нужна действующая подписка или активированный тест на 3 дня.";
        Assert.Equal(message, feedback.Display(message));
        Assert.Equal("Данные обновлены.", feedback.Display("Данные обновлены."));
    }
}
