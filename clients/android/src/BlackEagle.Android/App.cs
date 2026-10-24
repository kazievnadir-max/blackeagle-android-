using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using BlackEagle.Client.Core;
using BlackEagle.Client.UI;

namespace BlackEagle.Android;

public sealed class App : global::Avalonia.Application
{
    private ClientAccessClient? api;
    private MainViewModel? viewModel;
    private VpnClient? vpn;
    private MobileView? mobileView;
    private Task initialization = Task.CompletedTask;

    /// <summary>Подключает темную тему переносимого интерфейса.</summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    /// <summary>Создает мобильный экран и подключает его к рабочему API сервиса.</summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime mobile)
        {
            var store = new AndroidSessionStore();
            var address = new Uri("https://blackeagle-vpn.com/", UriKind.Absolute);
            api = new ClientAccessClient(address, store);
            vpn = new VpnClient(address, store, store, AndroidTunnel.Instance, devicePlatform: "android",
                adblockEnabled: () => MobilePreferences.AdBlockingEnabled);
            viewModel = new MainViewModel(api, store, TimeProvider.System, address.AbsoluteUri, vpn: vpn, probe: new AndroidServerProbe())
            {
                PlatformLabel = "Android"
            };
            mobileView = new MobileView(viewModel);
            mobile.MainView = mobileView;
            initialization = viewModel.InitializeAsync();
            _ = HandleTileRequestAsync();
        }
        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Выполняет запрос плитки только после подготовки профиля и экрана системного разрешения.</summary>
    internal async Task HandleTileRequestAsync()
    {
        await initialization;
        var tunnel = AndroidTunnel.Instance;
        if (tunnel.State != "disconnected")
            tunnel.Publish(tunnel.State, tunnel.State == "connected" ? "Подключено" : "Подключение…");
        if (viewModel is null || mobileView is null || !MainActivity.IsReady || !TileRequest.Consume()) return;
        if (AndroidTunnel.Instance.State == "disconnected" && viewModel.CanConnect && viewModel.CanSelectServer)
            await mobileView.ToggleConnectionAsync();
        // При отсутствии профиля или подписки оставляем открытым экран с объяснением.
    }

    /// <summary>Освобождает модель экрана и HTTP-клиент.</summary>
    public void DisposeServices()
    {
        viewModel?.Dispose();
        vpn?.Dispose();
        api?.Dispose();
        viewModel = null;
        api = null;
    }
}
