using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BlackEagle.NodeProtocol;

public sealed record AwgProfile(int Jc, int Jmin, int Jmax, int S1, int S2, uint H1, uint H2, uint H3, uint H4);
public sealed record NodeDefinition(string Name, string CountryCode, string City, string EndpointHost,
    int Port, string PublicKey, string Subnet, string Dns, AwgProfile Profile);
public sealed record NodePeer(Guid DeviceId, string PublicKey, string Address, DateTimeOffset AccessUntil);
public sealed record NodeSnapshot(Guid NodeId, string Revision, DateTimeOffset ValidUntil, NodeDefinition Definition, List<NodePeer> Peers);
public sealed record NodeHeartbeat(string Revision, int PeerCount);
public sealed record VpnConfiguration(Guid NodeId, Guid DeviceId, string Address, string Dns,
    string Endpoint, string ServerPublicKey, AwgProfile Profile, int Mtu = 1280,
    string AllowedIPs = "0.0.0.0/0, ::/0", int PersistentKeepalive = 25, string? Subnet = null,
    bool? AdblockEnabled = null);

public static class NodeValidation
{
    /// <summary>Проверяет канонический Base64 ключа длиной 32 байта и возвращает его hex-представление.</summary>
    public static string KeyHex(string value)
    {
        Span<byte> bytes = stackalloc byte[32];
        if (value is null || value.Length != 44 || !Convert.TryFromBase64String(value, bytes, out var length) ||
            length != 32 || Convert.ToBase64String(bytes) != value || bytes.IndexOfAnyExcept((byte)0) < 0)
            throw new ArgumentException("Invalid AWG key.");
        return Convert.ToHexStringLower(bytes);
    }

    /// <summary>Проверяет ограниченный совместимый AWG-профиль и сетевые параметры новой ноды.</summary>
    public static void Validate(NodeDefinition node)
    {
        if (node.Name is null || node.Name.Length is < 1 or > 80 || node.Name.Any(char.IsControl) ||
            node.City is null || node.City.Length is < 1 or > 80 || node.City.Any(char.IsControl) ||
            node.CountryCode is null || node.CountryCode.Length != 2 || node.CountryCode.Any(c => c is < 'A' or > 'Z'))
            throw new ArgumentException("Invalid node location.");
        if (node.Port is < 1 or > 65535 || node.EndpointHost is null || node.EndpointHost.Length > 253 ||
            node.EndpointHost.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '-')) ||
            Uri.CheckHostName(node.EndpointHost) is not (UriHostNameType.Dns or UriHostNameType.IPv4))
            throw new ArgumentException("Invalid endpoint host or port.");
        KeyHex(node.PublicKey);
        Capacity(node.Subnet);
        if (!IPAddress.TryParse(node.Dns, out var dns) || dns.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork || dns.ToString() != node.Dns)
            throw new ArgumentException("DNS must be a canonical IPv4 address.");
        var p = node.Profile ?? throw new ArgumentException("AWG profile is required.");
        if (p.Jc is < 0 or > 20 || p.Jmin is < 0 or > 1200 || p.Jmax < p.Jmin || p.Jmax > 1200 ||
            p.S1 is < 0 or > 128 || p.S2 is < 0 or > 128 || p.S1 + 148 == p.S2 + 92 ||
            new[] { p.H1, p.H2, p.H3, p.H4 }.Any(h => h == 0) || new[] { p.H1, p.H2, p.H3, p.H4 }.Distinct().Count() != 4)
            throw new ArgumentException("Invalid AWG profile.");
    }

    /// <summary>Возвращает границы частного пула /16 либо совместимого прежнего пула /24.</summary>
    private static (uint Network, int Prefix, int Capacity) Pool(string subnet)
    {
        var parts = subnet?.Split('/');
        if (parts is not { Length: 2 } || parts[1] is not ("16" or "24") ||
            !IPAddress.TryParse(parts[0], out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork ||
            ip.ToString() != parts[0]) throw new ArgumentException("A canonical private IPv4 /16 or /24 subnet is required.");
        var bytes = ip.GetAddressBytes();
        var prefix = int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
        if (!(bytes[0] == 10 || bytes[0] == 172 && bytes[1] is >= 16 and <= 31 || bytes[0] == 192 && bytes[1] == 168) ||
            bytes[3] != 0 || prefix == 16 && bytes[2] != 0) throw new ArgumentException("Invalid private subnet boundary.");
        return (System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes), prefix, (1 << (32 - prefix)) - 3);
    }

    /// <summary>Возвращает число адресов устройств без сети, шлюза и широковещательного адреса.</summary>
    public static int Capacity(string subnet) => Pool(subnet).Capacity;

    /// <summary>Строит канонический адрес из смещения относительно начала подсети.</summary>
    private static string Host(uint network, int offset)
    {
        Span<byte> bytes = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bytes, network + (uint)offset);
        return new IPAddress(bytes).ToString();
    }

    /// <summary>Возвращает адрес шлюза с маской интерфейса.</summary>
    public static string Gateway(string subnet)
    {
        var pool = Pool(subnet);
        return $"{Host(pool.Network, 1)}/{pool.Prefix}";
    }

    /// <summary>Формирует адрес устройства по смещению в пуле.</summary>
    public static string PeerAddress(string subnet, int offset)
    {
        var pool = Pool(subnet);
        if (offset < 2 || offset > pool.Capacity + 1) throw new ArgumentException("Invalid peer offset.");
        return Host(pool.Network, offset) + "/32";
    }

    /// <summary>Проверяет канонический адрес устройства и исключает зарезервированные адреса подсети.</summary>
    public static void ValidateAddress(string subnet, string address)
    {
        var pool = Pool(subnet);
        if (address is null || !address.EndsWith("/32", StringComparison.Ordinal) ||
            !IPAddress.TryParse(address[..^3], out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork ||
            ip + "/32" != address) throw new ArgumentException("Invalid peer address.");
        var value = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(ip.GetAddressBytes());
        if (value < pool.Network + 2 || value > pool.Network + pool.Capacity + 1) throw new ArgumentException("Peer outside pool.");
    }

    /// <summary>Вычисляет стабильную ревизию параметров и разрешенных peers, исключая время продления lease.</summary>
    public static string Revision(NodeDefinition definition, IReadOnlyList<NodePeer> peers)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            definition, peers = peers.OrderBy(p => p.DeviceId).ToArray()
        }))));
}
