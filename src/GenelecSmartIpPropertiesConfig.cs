using System.Collections.Generic;
using Newtonsoft.Json;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.Plugins
{
  /// <summary>
  /// Genelec Smart IP device configuration object
  /// </summary>
  /// <example>
  /// <code>
  /// {
  ///   "key": "speaker-1",
  ///   "name": "Lobby Speaker",
  ///   "type": "genelecSmartIp",
  ///   "group": "audio",
  ///   "properties": {
  ///     "host": "192.168.0.79",
  ///     "port": 9000,
  ///     "username": "admin",
  ///     "password": "admin",
  ///     "pollTimeMs": 5000,
  ///     "volumeMinDb": -130,
  ///     "volumeMaxDb": 0,
  ///     "volumeStepDb": 1.0,
  ///     "inputs": {
  ///       "analog": { "name": "Analog", "inputs": [ "A" ] },
  ///       "aoip": { "name": "AoIP 1+2", "inputs": [ "AoIP01", "AoIP02" ] }
  ///     }
  ///   }
  /// }
  /// </code>
  /// </example>
  [ConfigSnippet("\"properties\":{\"host\":\"\",\"port\":9000,\"username\":\"admin\",\"password\":\"admin\",\"pollTimeMs\":5000}")]
  public class GenelecSmartIpPropertiesConfig
  {
    /// <summary>
    /// IP address or hostname of the speaker (required)
    /// </summary>
    [JsonProperty("host")]
    public string Host { get; set; }

    /// <summary>
    /// Smart IP API port. Default 9000
    /// </summary>
    [JsonProperty("port")]
    public int Port { get; set; } = 9000;

    /// <summary>
    /// HTTP Basic auth user name. Default "admin"
    /// </summary>
    [JsonProperty("username")]
    public string Username { get; set; } = "admin";

    /// <summary>
    /// HTTP Basic auth password. Default "admin"
    /// </summary>
    [JsonProperty("password")]
    public string Password { get; set; } = "admin";

    /// <summary>
    /// Poll interval in ms. Default 5000. The API guide asks for polling to be kept as infrequent as possible
    /// </summary>
    [JsonProperty("pollTimeMs")]
    public long PollTimeMs { get; set; } = 5000;

    /// <summary>
    /// Per-request timeout in ms. Default 3000
    /// </summary>
    [JsonProperty("requestTimeoutMs")]
    public int RequestTimeoutMs { get; set; } = 3000;

    /// <summary>
    /// Level (dB) that maps to 0 on the 0-65535 volume scale. Default -130
    /// (the API documents -200..0 for unicast but -130..0 for multicast)
    /// </summary>
    [JsonProperty("volumeMinDb")]
    public double VolumeMinDb { get; set; } = -130;

    /// <summary>
    /// Level (dB) that maps to 65535 on the volume scale. Default 0. Lower this to cap the maximum level
    /// </summary>
    [JsonProperty("volumeMaxDb")]
    public double VolumeMaxDb { get; set; } = 0;

    /// <summary>
    /// dB change per VolumeUp/VolumeDown step. Default 1.0
    /// </summary>
    [JsonProperty("volumeStepDb")]
    public double VolumeStepDb { get; set; } = 1.0;

    /// <summary>
    /// Repeat interval in ms while a volume up/down button is held. Default 250
    /// </summary>
    [JsonProperty("volumeRampIntervalMs")]
    public int VolumeRampIntervalMs { get; set; } = 250;

    /// <summary>
    /// Optional fixed ("set and forget") level in dB, -200.0..0.0. When set, the plugin applies this level
    /// whenever the speaker is ACTIVE and its level differs (startup, comms recovery, wake from standby, or a
    /// change made at the speaker), and ignores volume set/up/down commands. Mute is unaffected.
    /// Omit (or null) to leave volume under normal control
    /// </summary>
    [JsonProperty("fixedLevelDb")]
    public double? FixedLevelDb { get; set; }

    /// <summary>
    /// Selectable inputs keyed by item key. If omitted/empty the defaults below are used
    /// </summary>
    [JsonProperty("inputs")]
    public Dictionary<string, GenelecSmartIpInputConfig> Inputs { get; set; }

    /// <summary>
    /// Default input items used when none are configured
    /// </summary>
    public static Dictionary<string, GenelecSmartIpInputConfig> DefaultInputs()
    {
      return new Dictionary<string, GenelecSmartIpInputConfig>
            {
                { "analog", new GenelecSmartIpInputConfig { Name = "Analog", Inputs = new List<string> { "A" } } },
                { "aoip1", new GenelecSmartIpInputConfig { Name = "AoIP 1", Inputs = new List<string> { "AoIP01" } } },
                { "aoip2", new GenelecSmartIpInputConfig { Name = "AoIP 2", Inputs = new List<string> { "AoIP02" } } },
                { "aoip1and2", new GenelecSmartIpInputConfig { Name = "AoIP 1+2", Inputs = new List<string> { "AoIP01", "AoIP02" } } }
            };
    }
  }
}
