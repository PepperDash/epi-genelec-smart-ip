using System.Collections.Generic;
using Newtonsoft.Json;

namespace PepperDash.Essentials.Plugins
{
  /// <summary>
  /// A selectable input as exposed to Essentials / mobile control. Maps a friendly item onto
  /// one or more physical Smart IP inputs (A, AoIP01, AoIP02) which are sent together in a single
  /// PUT /audio/inputs call.
  /// </summary>
  public class GenelecSmartIpInputConfig
  {
    /// <summary>
    /// Friendly name shown on the touchpanel
    /// </summary>
    [JsonProperty("name")]
    public string Name { get; set; }

    /// <summary>
    /// Smart IP input identifiers selected by this item. Valid values: "A", "AoIP01", "AoIP02"
    /// </summary>
    [JsonProperty("inputs")]
    public List<string> Inputs { get; set; } = new List<string>();
  }
}
