using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using PepperDash.Core;

namespace PepperDash.Essentials.Plugins
{
  /// <summary>
  /// A configured level/mute preset as listed by IDspPresets.Presets (key and display name)
  /// </summary>
  public class GenelecSmartIpPresetItem : IKeyName
  {
    /// <summary>Creates the item</summary>
    public GenelecSmartIpPresetItem(string key, string name)
    {
      Key = key;
      Name = name;
    }

    /// <inheritdoc />
    [JsonProperty("key")]
    public string Key { get; private set; }

    /// <inheritdoc />
    [JsonProperty("name")]
    public string Name { get; private set; }

    /// <summary>
    /// Builds the IDspPresets.Presets dictionary (case-insensitive keys) from presets normalized by
    /// <see cref="GenelecSmartIpPresets.Normalize"/>
    /// </summary>
    public static Dictionary<string, IKeyName> BuildList(IDictionary<string, GenelecSmartIpPresetConfig> presets)
    {
      var result = new Dictionary<string, IKeyName>(StringComparer.OrdinalIgnoreCase);
      foreach (var entry in presets)
        result[entry.Key] = new GenelecSmartIpPresetItem(entry.Key, GenelecSmartIpPresets.DisplayName(entry.Key, entry.Value));
      return result;
    }
  }
}
