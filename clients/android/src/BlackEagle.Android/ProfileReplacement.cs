using BlackEagle.Client.UI;

namespace BlackEagle.Android;

internal static class ProfileReplacement
{
    /// <summary>Останавливает старое соединение перед обменом новой ссылки, не удаляя сессию заранее.</summary>
    public static async Task ImportAsync(MainViewModel model)
    {
        if (model.IsTunnelActive) await model.ToggleConnectionAsync();
        if (!model.CanImport) throw new InvalidOperationException("Profile replacement is not ready.");
        await model.ImportAsync();
    }
}
