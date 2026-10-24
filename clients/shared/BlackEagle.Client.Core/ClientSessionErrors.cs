namespace BlackEagle.Client.Core;

internal static class ClientSessionErrors
{
    /// <summary>Передает ошибку API без удаления профиля по неоднозначному HTTP-статусу.</summary>
    public static Task CheckAsync(HttpClient http, HttpResponseMessage response, string path, string token,
        IClientSessionStore store, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!response.IsSuccessStatusCode) throw new ClientAccessException(response.StatusCode);
        return Task.CompletedTask;
    }
}
