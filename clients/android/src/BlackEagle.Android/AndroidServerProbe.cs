using System.Net;
using System.Net.Sockets;
using BlackEagle.Client.Core;

namespace BlackEagle.Android;

internal sealed class AndroidServerProbe : IServerProbe
{
    /// <summary>Измеряет ICMP системной утилитой Android без raw-сокета, root и пользовательского payload.</summary>
    public async Task<long?> MeasureAsync(string host, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(host) || host.Length > 253 ||
            Uri.CheckHostName(host) is not (UriHostNameType.Dns or UriHostNameType.IPv4)) return null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(7));
        try
        {
            var address = (await Dns.GetHostAddressesAsync(host, timeout.Token))
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            if (address is null) return null;
            return await Task.Run(async () =>
            {
                timeout.Token.ThrowIfCancellationRequested();
                // Только числовой IPv4, отдельные аргументы; shell не запускается.
                using var builder = new Java.Lang.ProcessBuilder(new[]
                    { "/system/bin/ping", "-n", "-c", "3", "-W", "1", "-w", "4", address.ToString() });
                builder.RedirectErrorStream(true);
                using var process = builder.Start() ?? throw new IOException("Не удалось запустить ping.");
                using var cancellation = timeout.Token.Register(() => process.Destroy());
                try
                {
                    using var reader = new StreamReader(process.InputStream!);
                    var output = await reader.ReadToEndAsync(timeout.Token);
                    timeout.Token.ThrowIfCancellationRequested();
                    return PingOutput.AverageMilliseconds(output);
                }
                finally { process.Destroy(); }
            }, timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return null; }
        catch (Exception ex) when (ex is SocketException or IOException or Java.IO.IOException or Java.Lang.SecurityException) { return null; }
    }
}
