using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace BlackEagle.Client.Core;

public interface IServerProbe
{
    /// <summary>Измеряет ICMP RTT либо возвращает отсутствие ответа.</summary>
    Task<long?> MeasureAsync(string host, CancellationToken ct);
}

public sealed class ServerProbe : IServerProbe
{
    /// <summary>Измеряет средний IPv4 RTT трех проб без регистрации VPN-устройства.</summary>
    public async Task<long?> MeasureAsync(string host, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(host) || host.Length > 253 || Uri.CheckHostName(host) is not (UriHostNameType.Dns or UriHostNameType.IPv4)) return null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(6));
        try
        {
            var address = (await Dns.GetHostAddressesAsync(host, timeout.Token)).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            if (address is null) return null;
            using var ping = new Ping(); var samples = new List<long>();
            for (var i = 0; i < 3; i++)
            {
                var reply = await ping.SendPingAsync(address, TimeSpan.FromSeconds(1), new byte[16], null, timeout.Token);
                if (reply.Status == IPStatus.Success) samples.Add(reply.RoundtripTime);
            }
            return samples.Count == 0 ? null : (long)Math.Round(samples.Average());
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return null; }
        catch (Exception ex) when (ex is PingException or SocketException or PlatformNotSupportedException) { return null; }
    }
}
