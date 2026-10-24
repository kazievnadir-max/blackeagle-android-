using Xunit;

namespace BlackEagle.Android;

public sealed class PingOutputTests
{
    /// <summary>Проверяет дробные ответы, частичную потерю пакетов и отсутствие фиктивного пинга при ошибках.</summary>
    [Theory]
    [InlineData("64 bytes from 192.0.2.1: icmp_seq=1 ttl=53 time=32.4 ms\n64 bytes from 192.0.2.1: icmp_seq=3 ttl=53 time=35.6 ms", 34L)]
    [InlineData("64 bytes from 192.0.2.1: icmp_seq=1 ttl=53 time=12,5 ms", 12L)]
    [InlineData("64 bytes from 192.0.2.1: icmp_seq=1 ttl=53 time<1 ms", 1L)]
    [InlineData("3 packets transmitted, 0 received, 100% packet loss, time 3000ms", null)]
    [InlineData("ping: socket: Operation not permitted", null)]
    [InlineData("From 192.0.2.1 icmp_seq=1 Destination Host Unreachable", null)]
    public void ParsesOnlyReplies(string output, long? expected) => Assert.Equal(expected, PingOutput.AverageMilliseconds(output));
}
