using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using BlackEagle.Client.Core;

namespace BlackEagle.Client.UI;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IClientAccessClient api;
    private readonly IClientSessionStore store;
    private readonly TimeProvider clock;
    private readonly CancellationTokenSource lifetime = new();
    private readonly bool configured;
    private string link = "";
    private bool busy;
    private ClientSession? session;
    private bool requiresActivation;
    private bool stale;
    private string message;
    private readonly IVpnClient? vpn;
    private readonly IServerProbe probe;
    private readonly SynchronizationContext? context = SynchronizationContext.Current;
    private CancellationTokenSource? connecting;
    private ServerRow? selectedServer;
    private bool adding, profile, probing, tunnelActive, disconnecting;
    private bool tunnelConnected;
    private bool renewingSession;
    private DateTimeOffset nextSessionRenewal;
    private bool CanRenewSession => session?.RefreshToken is not null && session.RefreshExpiresAt > clock.GetUtcNow();
    public ObservableCollection<ServerRow> Servers { get; } = [];
    public event PropertyChangedEventHandler? PropertyChanged;
    public string ApiAddress { get; }
    public string PlatformLabel { get; init; } = "";
    public ServerRow? SelectedServer { get => selectedServer; set { selectedServer = value; Changed(); Changed(nameof(CanConnect)); } }
    public bool IsAdding { get => adding; set { adding = value; if (!value) Link = ""; Changed(); Changed(nameof(IsDialogOpen)); } }
    public bool IsProfileOpen { get => profile; set { profile = value; Changed(); Changed(nameof(IsDialogOpen)); } }
    public bool IsDialogOpen => IsAdding || IsProfileOpen;
    public bool CanSelectServer => !IsBusy && !tunnelActive && connecting is null;
    public bool CanConnect => vpn is not null && !disconnecting && (tunnelActive || (!IsBusy && !probing && connecting is null && SelectedServer is not null && (session?.ExpiresAt > clock.GetUtcNow() || CanRenewSession) && session?.Account.AccessUntil > clock.GetUtcNow() && !stale));
    public bool CanProbe => !probing && !IsBusy && HasServers && !tunnelActive;
    public bool IsTunnelActive => tunnelActive;
    public bool IsTunnelConnected => tunnelConnected;
    public DateTimeOffset? ConnectedSince { get; private set; }
    public string ConnectText => tunnelActive ? "Отключиться" : "Подключиться";
    public string TunnelText { get; private set; } = "Не подключено";
    public string Link { get => link; set { link = value; Changed(); Changed(nameof(CanImport)); } }
    public string Message { get => message; private set { message = value; Changed(); } }
    public bool IsBusy { get => busy; private set { busy = value; NotifyState(); } }
    public bool CanImport => configured && !IsBusy && !tunnelActive && connecting is null && !string.IsNullOrWhiteSpace(Link);
    public bool CanRefresh => configured && !IsBusy && !tunnelActive && connecting is null && session is not null;
    public bool CanLogout => !IsBusy && !tunnelActive && connecting is null && session is not null;
    public bool CanEdit => !IsBusy;
    public bool HasServers => Servers.Count > 0;
    public string ServerCount => $"Локаций: {Servers.Count}";
    public string AccountText => session is null ? "Войдите по ссылке из Telegram" : $"Telegram ID: {session.Account.TelegramId}";
    public string SubscriptionStatus => session is null ? "Нет данных" :
        session.ExpiresAt <= clock.GetUtcNow() && !CanRenewSession ? "Сессия истекла" :
        requiresActivation || session.Account.AccessUntil is not { } until || until <= clock.GetUtcNow() ? "Доступ истёк" :
        stale ? "Требуется обновление" : "Подписка активна";
    public string SubscriptionDetail => session?.Account.AccessUntil is { } until
        ? $"Доступ до {until.ToLocalTime():dd.MM.yyyy HH:mm} (местное время)"
        : "Подписка или тестовый период на 3 дня активируются в боте.";
    public string EmptyText => session is null ? "После входа здесь появятся ваши серверы." :
        stale ? "Не удалось получить актуальный список. Нажмите «Обновить»." :
        requiresActivation || session.Account.AccessUntil <= clock.GetUtcNow() ? "Для просмотра серверов продлите доступ в Telegram." :
        "Доступных серверов пока нет. Попробуйте обновить список позже.";

    /// <summary>Создает переносимую модель экрана без зависимости от Linux и окон Avalonia.</summary>
    public MainViewModel(IClientAccessClient api, IClientSessionStore store, TimeProvider clock, string apiAddress, bool configured = true,
        IVpnClient? vpn = null, IServerProbe? probe = null)
    {
        this.api = api; this.store = store; this.clock = clock; this.configured = configured;
        ApiAddress = apiAddress;
        this.vpn = vpn; this.probe = probe ?? new ServerProbe();
        if (vpn is not null) vpn.StatusChanged += TunnelChanged;
        message = configured ? "Нажмите +, чтобы добавить подписку по ссылке из Telegram."
            : "API не настроен. Укажите BLACK_EAGLE_API_URL по инструкции README.";
    }

    /// <summary>Восстанавливает сессию и выбор сервера без автоматического подключения.</summary>
    public async Task InitializeAsync()
    {
        try
        {
            session = await store.LoadAsync(lifetime.Token);
            if (session is not null)
            {
                await RefreshAsync();
                var last = vpn is null ? null : await vpn.LastServerAsync(lifetime.Token);
                SelectedServer = Servers.FirstOrDefault(s => s.Id == last) ?? Servers.FirstOrDefault();
            }
        }
        catch (Exception) { Message = "Не удалось открыть защищенное хранилище сессии."; }
        NotifyState();
    }

    /// <summary>Подключает выбранную локацию либо отменяет соединение и ожидает очистки сети.</summary>
    public async Task ToggleConnectionAsync()
    {
        if (vpn is null || !CanConnect) return;
        if (tunnelActive) { await StopTunnelAsync(); return; }
        connecting = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        tunnelActive = true; TunnelText = "Подключение… Подтвердите системный запрос."; NotifyState();
        try { await vpn.ConnectAsync(SelectedServer!.Id, connecting.Token); }
        catch (Exception)
        {
            tunnelActive = false; TunnelText = "Не подключено";
            Message = "Не удалось подключиться. Проверьте разрешение VPN, подписку и доступность сервера.";
        }
        finally { connecting.Dispose(); connecting = null; NotifyState(); }
    }

    /// <summary>Отключает VPN независимо от доступности API, исключая одновременные операции очистки.</summary>
    private async Task StopTunnelAsync()
    {
        if (disconnecting) return;
        disconnecting = true; connecting?.Cancel(); NotifyState();
        try { await vpn!.DisconnectAsync(lifetime.Token); tunnelActive = false; tunnelConnected = false; ConnectedSince = null; TunnelText = "Отключено"; }
        catch (Exception) { Message = "Не удалось подтвердить отключение. Проверьте состояние helper."; }
        finally { disconnecting = false; NotifyState(); }
    }

    /// <summary>Переносит события платформы на контекст UI.</summary>
    private void TunnelChanged(object? sender, TunnelStatus value)
    {
        if (context is not null && SynchronizationContext.Current != context)
        { context.Post(_ => ApplyTunnel(value), null); return; }
        ApplyTunnel(value);
    }

    /// <summary>Показывает соединение по реальному состоянию AWG.</summary>
    private void ApplyTunnel(TunnelStatus value)
    {
        if (lifetime.IsCancellationRequested) return;
        tunnelActive = value.State is "connected" or "reconnecting" or "connecting";
        tunnelConnected = value.State == "connected";
        if (value.State == "connected" && ConnectedSince is null) ConnectedSince = clock.GetUtcNow();
        if (!tunnelActive) ConnectedSince = null;
        TunnelText = value.Message; NotifyState();
    }

    /// <summary>Показывает безопасное пользовательское сообщение от платформенного интерфейса.</summary>
    public void ReportMessage(string value) => Message = value;

    /// <summary>Измеряет ICMP-пинг всего каталога с ограничением параллелизма.</summary>
    public async Task ProbeAsync()
    {
        if (!CanProbe) return;
        probing = true; NotifyState();
        using var limit = new SemaphoreSlim(4);
        try
        {
            await Task.WhenAll(Servers.ToArray().Select(async row =>
            {
                await limit.WaitAsync(lifetime.Token);
                try { row.PingText = "…"; var ping = row.EndpointHost is null ? null : await probe.MeasureAsync(row.EndpointHost, lifetime.Token);
                    row.PingText = ping is { } ms ? $"{ms} мс" : "Нет ICMP"; }
                finally { limit.Release(); }
            }));
            Message = "Пинг измерен по ICMP. «Нет ICMP» не означает, что VPN-сервер недоступен.";
        }
        catch (OperationCanceledException) { }
        catch (Exception) { Message = "Не удалось измерить ICMP-пинг."; }
        finally { probing = false; NotifyState(); }
    }

    /// <summary>Импортирует одноразовую ссылку, удаляя ее из поля после попытки и обновляя данные сессии.</summary>
    public async Task ImportAsync()
    {
        if (!CanImport) return;
        var value = Link;
        Link = "";
        await RunAsync(async ct => Apply(await api.ImportAsync(value, ct)), "Вход выполнен. Список серверов обновлён.");
        if (!stale) IsAdding = false;
    }

    /// <summary>Обновляет профиль и каталог по сохраненной сессии, не погашая ссылку повторно.</summary>
    public async Task RefreshAsync()
    {
        if (!CanRefresh) return;
        await RunAsync(async ct => Apply(await api.RefreshAsync(ct)), "Данные обновлены.");
    }

    /// <summary>Отзывает только сессию данного приложения и очищает экран после подтверждения API.</summary>
    public async Task LogoutAsync()
    {
        if (!CanLogout) return;
        await RunAsync(async ct => { await api.LogoutAsync(ct); Servers.Clear(); requiresActivation = false; }, "Вы вышли. Для входа получите новую ссылку в Telegram.");
    }

    /// <summary>Выполняет одну операцию, обрабатывая ошибки без вывода токенов и технических ответов API.</summary>
    private async Task RunAsync(Func<CancellationToken, Task> operation, string success)
    {
        if (IsBusy) return;
        IsBusy = true;
        Message = "Обращаемся к сервису…";
        try
        {
            await operation(lifetime.Token);
            stale = false;
            Message = requiresActivation ? "Доступ закончился. Продлите подписку в Telegram." : success;
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { return; }
        catch (Exception ex)
        {
            stale = true;
            Servers.Clear();
            Message = ex switch
            {
                ArgumentException => "Ссылка не подходит этому сервису. Скопируйте полную ссылку из Telegram.",
                ClientAccessException { Status: HttpStatusCode.Unauthorized } => "Ссылка или сессия истекла либо уже использована. Получите новую ссылку в боте.",
                ClientAccessException { Status: HttpStatusCode.Forbidden } => "Нужна действующая подписка или активированный тест на 3 дня.",
                ClientAccessException { Status: HttpStatusCode.TooManyRequests } => "Слишком много запросов. Подождите минуту.",
                _ => "Сервис недоступен. Попробуйте «Обновить». Если сессия не получена — запросите новую ссылку."
            };
        }
        finally
        {
            // API мог сохранить сессию до сбоя каталога: оставляем доступной кнопку обновления.
            try { session = await store.LoadAsync(CancellationToken.None); }
            catch (Exception) { Message = "Связка ключей недоступна. Проверьте Secret Service."; }
            IsBusy = false;
            Tick();
        }
    }

    /// <summary>Заменяет список серверов данными API без создания фиктивных локаций.</summary>
    private void Apply(AccessResult result)
    {
        requiresActivation = result.RequiresActivation;
        var previous = SelectedServer?.Id;
        Servers.Clear();
        foreach (var server in result.Servers) Servers.Add(new(server));
        SelectedServer = Servers.FirstOrDefault(s => s.Id == previous) ?? Servers.FirstOrDefault();
    }

    /// <summary>Обновляет показ срока и скрывает серверы при локальном истечении доступа или сессии.</summary>
    public void Tick()
    {
        if (session is not null && !IsBusy && !renewingSession && clock.GetUtcNow() >= nextSessionRenewal &&
            (session.RefreshToken is null || session.RefreshUpgradePending || session.ExpiresAt <= clock.GetUtcNow().AddDays(1)) && api is IClientSessionRenewal renewal)
            _ = RenewSessionAsync(renewal);
        if (session is not null && ((session.ExpiresAt <= clock.GetUtcNow() && !CanRenewSession) || session.Account.AccessUntil is not { } until || until <= clock.GetUtcNow()))
        {
            Servers.Clear();
            SelectedServer = null;
            if (tunnelActive && vpn is not null) _ = StopTunnelAsync();
        }
        NotifyState();
    }

    /// <summary>Заранее продлевает сессию без отключения VPN и ограничивает повторы при недоступности API.</summary>
    private async Task RenewSessionAsync(IClientSessionRenewal renewal)
    {
        renewingSession = true;
        nextSessionRenewal = clock.GetUtcNow().AddMinutes(5);
        var previousToken = session?.AccessToken;
        try
        {
            var updated = await renewal.RenewSessionAsync(lifetime.Token);
            if (session?.AccessToken == previousToken) session = updated;
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception) { /* Сохраняем профиль и повторяем позже; права на VPN проверяются сервером отдельно. */ }
        finally { renewingSession = false; NotifyState(); }
    }

    /// <summary>Уведомляет представление об изменении зависимых свойств.</summary>
    private void NotifyState()
    {
        foreach (var name in new[] { nameof(IsBusy), nameof(CanImport), nameof(CanRefresh), nameof(CanLogout), nameof(CanEdit),
            nameof(HasServers), nameof(ServerCount), nameof(AccountText), nameof(SubscriptionStatus), nameof(SubscriptionDetail), nameof(EmptyText),
            nameof(CanSelectServer), nameof(CanConnect), nameof(CanProbe), nameof(IsTunnelActive), nameof(IsTunnelConnected), nameof(ConnectedSince), nameof(ConnectText), nameof(TunnelText) })
            Changed(name);
    }

    /// <summary>Отправляет уведомление об изменении одного свойства модели.</summary>
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));

    /// <summary>Отменяет незавершенные запросы при закрытии приложения.</summary>
    public void Dispose() { if (vpn is not null) vpn.StatusChanged -= TunnelChanged; lifetime.Cancel(); lifetime.Dispose(); }
}

public sealed class ServerRow(ServerInfo server) : INotifyPropertyChanged
{
    private string ping = "—";
    public Guid Id => server.Id;
    public string Name => server.Name;
    public string CountryCode => server.CountryCode;
    public string City => server.City;
    public string? EndpointHost => server.EndpointHost;
    public bool AdblockSupported => server.AdblockSupported;
    public string PingText { get => ping; set { ping = value; PropertyChanged?.Invoke(this, new(nameof(PingText))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}
