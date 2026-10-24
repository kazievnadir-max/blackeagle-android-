using System.Net;
using System.Net.Sockets;
using BlackEagle.NodeProtocol;

namespace BlackEagle.Client.Core;

public static class AndroidAwgConfiguration
{
    /// <summary>Формирует проверенные параметры UAPI для Android с заранее разрешенным IPv4 endpoint.</summary>
    public static string Build(TunnelRequest request, IPAddress endpoint)
    {
        TunnelValidation.Validate(request);
        if (endpoint.AddressFamily != AddressFamily.InterNetwork)
            throw new ArgumentException("Требуется IPv4 endpoint.");
        var c = request.Configuration;
        var p = c.Profile;
        var port = c.Endpoint.Split(':')[1];
        return FormattableString.Invariant($"private_key={NodeValidation.KeyHex(request.PrivateKey)}\njc={p.Jc}\njmin={p.Jmin}\njmax={p.Jmax}\ns1={p.S1}\ns2={p.S2}\ns3=0\ns4=0\nh1={p.H1}\nh2={p.H2}\nh3={p.H3}\nh4={p.H4}\nreplace_peers=true\npublic_key={NodeValidation.KeyHex(c.ServerPublicKey)}\nendpoint={endpoint}:{port}\nallowed_ip=0.0.0.0/0\npersistent_keepalive_interval={c.PersistentKeepalive}\n");
    }
}
