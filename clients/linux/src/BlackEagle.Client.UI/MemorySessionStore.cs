using BlackEagle.Client.Core;

namespace BlackEagle.Client.UI;

public sealed class MemorySessionStore : IClientSessionStore
{
    private ClientSession? session;
    /// <summary>Сохраняет сессию только в памяти текущего процесса, не записывая секрет на диск.</summary>
    public Task SaveAsync(ClientSession value, CancellationToken ct) { session = value; return Task.CompletedTask; }
    /// <summary>Читает сессию текущего процесса.</summary>
    public Task<ClientSession?> LoadAsync(CancellationToken ct) => Task.FromResult(session);
    /// <summary>Забывает сессию текущего процесса.</summary>
    public Task ClearAsync(CancellationToken ct) { session = null; return Task.CompletedTask; }
}
