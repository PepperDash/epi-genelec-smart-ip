// linked into the nullable-enabled test project; the plugin itself does not use nullable annotations
#nullable disable

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace PepperDash.Essentials.Plugins
{
  /// <summary>
  /// Sends Smart IP multicast messages over UDP. Each message is sent <c>count</c> times, <c>gapMs</c> apart:
  /// multicast has no acknowledgement and the messages are idempotent, so a repeat covers a lost datagram.
  /// TTL 1 keeps the traffic on the local network. Pure .NET sockets (no Crestron dependencies).
  /// </summary>
  public class GenelecSmartIpMulticastSender : IDisposable
  {
    private readonly UdpClient client;
    private readonly object sync = new object();

    /// <param name="localAddress">optional IPv4 address of the interface to send from (e.g. the processor's
    /// LAN address when it also has a control subnet); empty lets the OS choose</param>
    /// <param name="ttl">multicast time-to-live; 1 = local network only</param>
    public GenelecSmartIpMulticastSender(string localAddress, int ttl = 1)
    {
      IPAddress local = IPAddress.Any;
      if (!string.IsNullOrWhiteSpace(localAddress) && !IPAddress.TryParse(localAddress.Trim(), out local))
        throw new ArgumentException("localAddress is not an IP address: " + localAddress, "localAddress");

      client = new UdpClient(new IPEndPoint(local, 0));
      client.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, Math.Max(1, ttl));
      if (!local.Equals(IPAddress.Any))
        client.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, local.GetAddressBytes());
    }

    /// <summary>
    /// Sends <paramref name="payload"/> to <paramref name="target"/> <paramref name="count"/> times. Returns
    /// null on success, otherwise the error text (the caller logs it; nothing is thrown).
    /// </summary>
    public string Send(IPEndPoint target, byte[] payload, int count = 2, int gapMs = 20)
    {
      if (target == null) throw new ArgumentNullException("target");
      if (payload == null) throw new ArgumentNullException("payload");
      lock (sync)
      {
        try
        {
          for (var i = 0; i < Math.Max(1, count); i++)
          {
            if (i > 0 && gapMs > 0) Thread.Sleep(gapMs);
            client.Send(payload, payload.Length, target);
          }
          return null;
        }
        catch (Exception ex) when (ex is SocketException || ex is ObjectDisposedException)
        {
          return ex.Message;
        }
      }
    }

    /// <inheritdoc />
    public void Dispose()
    {
      client.Dispose();
    }
  }
}
