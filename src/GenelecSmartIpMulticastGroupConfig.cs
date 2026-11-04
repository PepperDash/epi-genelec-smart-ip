using System.Collections.Generic;
using Newtonsoft.Json;

namespace PepperDash.Essentials.Plugins
{
  /// <summary>
  /// Config for the multicast group device (type "genelecSmartIpMulticastGroup")
  /// </summary>
  /// <example>
  /// <code>
  /// {
  ///   "key": "room-audio", "name": "Room Audio", "type": "genelecSmartIpMulticastGroup", "group": "audio",
  ///   "properties": {
  ///     "volumeGroup": { "address": "239.10.10.1", "port": 49152, "members": [ "genelec-front-left", "genelec-front-right" ] },
  ///     "muteGroup":   { "address": "239.10.10.2", "port": 49153, "members": [ "genelec-subwoofer" ] },
  ///     "volumeMinDb": -60, "volumeMaxDb": 0, "volumeStepDb": 1.0, "volumeRampIntervalMs": 250,
  ///     "localAddress": "", "sendCount": 2
  ///   }
  /// }
  /// </code>
  /// </example>
  public class GenelecSmartIpMulticastGroupConfig
  {
    /// <summary>Multicast group for level and mute (required). Its speakers get every volume/mute/power message</summary>
    [JsonProperty("volumeGroup")]
    public GenelecSmartIpMulticastGroupTarget VolumeGroup { get; set; }

    /// <summary>
    /// Optional multicast group for mute (and power) only, e.g. a fixed-level subwoofer on its own address so it
    /// never receives level messages. Omit when every speaker follows the volume
    /// </summary>
    [JsonProperty("muteGroup")]
    public GenelecSmartIpMulticastGroupTarget MuteGroup { get; set; }

    /// <summary>Level (dB) that maps to 0 on the group's 0-65535 volume scale. Default -60; clamped to -130</summary>
    [JsonProperty("volumeMinDb")]
    public double VolumeMinDb { get; set; } = -60;

    /// <summary>Level (dB) that maps to 65535. Default 0</summary>
    [JsonProperty("volumeMaxDb")]
    public double VolumeMaxDb { get; set; } = 0;

    /// <summary>dB per VolumeUp/VolumeDown step. Default 1.0</summary>
    [JsonProperty("volumeStepDb")]
    public double VolumeStepDb { get; set; } = 1.0;

    /// <summary>Repeat interval in ms while volume up/down is held. Default 250</summary>
    [JsonProperty("volumeRampIntervalMs")]
    public int VolumeRampIntervalMs { get; set; } = 250;

    /// <summary>
    /// Optional IPv4 address of the processor interface to send from. Set it to the LAN address on a processor
    /// that also has a control subnet, so the multicast leaves on the speakers' network. Empty: OS chooses
    /// </summary>
    [JsonProperty("localAddress")]
    public string LocalAddress { get; set; }

    /// <summary>Times each message is sent (20 ms apart); multicast has no acknowledgement. Default 2</summary>
    [JsonProperty("sendCount")]
    public int SendCount { get; set; } = 2;

    /// <summary>
    /// How long after a command to wait before comparing members' polled values with the group and re-sending a
    /// value a member missed. Default 6000 (one 5 s poll plus margin)
    /// </summary>
    [JsonProperty("repairDelayMs")]
    public int RepairDelayMs { get; set; } = 6000;
  }

  /// <summary>
  /// One multicast group: where messages are sent, and which genelecSmartIp devices listen there
  /// </summary>
  public class GenelecSmartIpMulticastGroupTarget
  {
    /// <summary>IPv4 multicast address, equal to the speakers' volIp (set in Smart IP Manager)</summary>
    [JsonProperty("address")]
    public string Address { get; set; }

    /// <summary>UDP port, equal to the speakers' volPort</summary>
    [JsonProperty("port")]
    public int Port { get; set; }

    /// <summary>
    /// Keys of the genelecSmartIp devices on this group. They provide feedback (the multicast API has none),
    /// let the group re-send a missed value, and are checked for a matching volIp/volPort
    /// </summary>
    [JsonProperty("members")]
    public List<string> Members { get; set; } = new List<string>();
  }
}
