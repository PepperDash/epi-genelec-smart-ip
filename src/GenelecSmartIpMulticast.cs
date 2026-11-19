// linked into the nullable-enabled test project; the plugin itself does not use nullable annotations
#nullable disable

using System;
using System.Globalization;
using System.Net;
using System.Text;

namespace PepperDash.Essentials.Plugins
{
  /// <summary>
  /// Pure-logic helpers for the Smart IP multicast API (Smart IP API Documentation v1 rev 0.8.4, sections 5-6).
  /// No Crestron dependencies so they can be unit tested.
  /// </summary>
  public static class GenelecSmartIpMulticast
  {
    /// <summary>Lowest level a multicast "level" message accepts (dB)</summary>
    public const double MinDb = -130.0;

    /// <summary>Highest level a multicast "level" message accepts (dB)</summary>
    public const double MaxDb = 0.0;

    /// <summary>Two levels within this many dB are the same at the device's 0.1 dB resolution</summary>
    public const double LevelToleranceDb = 0.05;

    /// <summary>Power states the multicast "state" message accepts</summary>
    public const string StateStandby = "STANDBY";

    /// <summary>Wakes the device (multicast has no ACTIVE; the device is booted)</summary>
    public const string StateBoot = "BOOT";

    /// <summary>Clamps to the multicast level range and rounds to 0.1 dB</summary>
    public static double ClampDb(double db)
    {
      if (double.IsNaN(db)) return MinDb;
      return Math.Round(Math.Max(MinDb, Math.Min(MaxDb, db)), 1);
    }

    /// <summary>{"mcast":{"ver":1,"level":-30.5}} with the level clamped to -130..0 and rounded to 0.1 dB</summary>
    public static byte[] LevelMessage(double db)
    {
      return Wrap("\"level\":" + ClampDb(db).ToString("0.0", CultureInfo.InvariantCulture));
    }

    /// <summary>{"mcast":{"ver":1,"mute":true}}</summary>
    public static byte[] MuteMessage(bool mute)
    {
      return Wrap("\"mute\":" + (mute ? "true" : "false"));
    }

    /// <summary>{"mcast":{"ver":1,"state":"STANDBY"}} or "BOOT"</summary>
    public static byte[] PowerMessage(string state)
    {
      if (state != StateStandby && state != StateBoot)
        throw new ArgumentException("multicast power state must be STANDBY or BOOT", "state");
      return Wrap("\"state\":\"" + state + "\"");
    }

    private static byte[] Wrap(string pair)
    {
      return Encoding.UTF8.GetBytes("{\"mcast\":{\"ver\":1," + pair + "}}");
    }

    /// <summary>True when two levels differ at the device's 0.1 dB resolution</summary>
    public static bool LevelDiffers(double a, double b)
    {
      return Math.Abs(a - b) > LevelToleranceDb;
    }

    /// <summary>
    /// Null when address/port make a usable multicast group, otherwise why not. The address must be IPv4
    /// multicast (224.0.0.0-239.255.255.255); the port 1024-65535 (the API doc suggests 49152-65535).
    /// </summary>
    public static string ValidateGroup(string address, int port)
    {
      IPAddress ip;
      if (string.IsNullOrWhiteSpace(address) || !IPAddress.TryParse(address.Trim(), out ip)
          || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        return "address must be an IPv4 address";
      var first = ip.GetAddressBytes()[0];
      if (first < 224 || first > 239)
        return "address " + address + " is not multicast (224.0.0.0-239.255.255.255)";
      if (port < 1024 || port > 65535)
        return "port " + port + " is outside 1024-65535";
      return null;
    }

    /// <summary>
    /// Compares a speaker's configured multicast group (from GET /network/ipv4 volIp/volPort) with the group it
    /// is listed in. Null when they match, otherwise a short description of the mismatch.
    /// </summary>
    public static string CompareSpeakerGroup(string speakerAddress, int? speakerPort, string groupAddress, int groupPort)
    {
      if (string.IsNullOrWhiteSpace(speakerAddress) || !speakerPort.HasValue) return "speaker did not report volIp/volPort";
      if (speakerAddress.Trim() == "0.0.0.0") return "multicast is disabled on the speaker (volIp 0.0.0.0)";
      var sameAddress = string.Equals(speakerAddress.Trim(), (groupAddress ?? "").Trim(), StringComparison.Ordinal);
      if (sameAddress && speakerPort.Value == groupPort) return null;
      return "speaker listens on " + speakerAddress.Trim() + ":" + speakerPort.Value + ", group sends to "
             + groupAddress + ":" + groupPort;
    }
  }
}
