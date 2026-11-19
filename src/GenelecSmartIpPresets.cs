// nullable off: also compiled into the test project, which enables nullable reference types
#nullable disable

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace PepperDash.Essentials.Plugins
{
  /// <summary>
  /// One level/mute preset from the optional "presets" config object, recalled through IDspPresets
  /// (e.g. a room's startup or shutdown preset). Each field is optional: only the ones present are applied
  /// </summary>
  /// <example>
  /// <code>
  /// "presets": {
  ///   "startup":  { "name": "Startup", "levelDb": -25, "mute": false },
  ///   "shutdown": { "name": "Shutdown", "mute": true }
  /// }
  /// </code>
  /// </example>
  public class GenelecSmartIpPresetConfig
  {
    /// <summary>Display name. Defaults to the preset key</summary>
    [JsonProperty("name")]
    public string Name { get; set; }

    /// <summary>Optional level in dB, clamped to the device's volumeMinDb..volumeMaxDb. Omit to leave the level alone</summary>
    [JsonProperty("levelDb")]
    public double? LevelDb { get; set; }

    /// <summary>Optional mute state. Omit to leave mute alone</summary>
    [JsonProperty("mute")]
    public bool? Mute { get; set; }
  }

  /// <summary>
  /// Pure-logic preset handling (no Crestron dependencies so it can be unit tested): key normalization and
  /// recall dispatch. The devices pass their own level and mute write paths to <see cref="Recall"/>
  /// </summary>
  public static class GenelecSmartIpPresets
  {
    /// <summary>
    /// Copies the configured presets into a case-insensitive dictionary keyed by the trimmed key. Blank keys and
    /// null entries are skipped; of keys that differ only in case the first wins. Each problem is passed to
    /// <paramref name="warn"/>. Null config gives an empty dictionary
    /// </summary>
    public static Dictionary<string, GenelecSmartIpPresetConfig> Normalize(
        IDictionary<string, GenelecSmartIpPresetConfig> presets, Action<string> warn = null)
    {
      var result = new Dictionary<string, GenelecSmartIpPresetConfig>(StringComparer.OrdinalIgnoreCase);
      if (presets == null) return result;

      foreach (var entry in presets)
      {
        var key = entry.Key == null ? string.Empty : entry.Key.Trim();
        if (key.Length == 0)
        {
          if (warn != null) warn("presets: an entry with a blank key is ignored");
          continue;
        }
        if (entry.Value == null)
        {
          if (warn != null) warn("presets: '" + key + "' has no settings and is ignored");
          continue;
        }
        if (result.ContainsKey(key))
        {
          if (warn != null) warn("presets: '" + key + "' duplicates another key (keys are case-insensitive) and is ignored");
          continue;
        }
        if (!entry.Value.LevelDb.HasValue && !entry.Value.Mute.HasValue && warn != null)
          warn("presets: '" + key + "' has neither levelDb nor mute, so recalling it does nothing");
        result.Add(key, entry.Value);
      }
      return result;
    }

    /// <summary>Display name of a preset: its name, or its key when no name is set</summary>
    public static string DisplayName(string key, GenelecSmartIpPresetConfig preset)
    {
      return preset != null && !string.IsNullOrWhiteSpace(preset.Name) ? preset.Name.Trim() : key;
    }

    /// <summary>
    /// The level a preset sets, clamped to minDb..maxDb and rounded to 0.1 dB; null when the preset has no
    /// (finite) level
    /// </summary>
    public static double? Level(GenelecSmartIpPresetConfig preset, double minDb, double maxDb)
    {
      if (preset == null || !preset.LevelDb.HasValue) return null;
      var db = preset.LevelDb.Value;
      if (double.IsNaN(db) || double.IsInfinity(db)) return null;
      var lo = Math.Min(minDb, maxDb);
      var hi = Math.Max(minDb, maxDb);
      return Math.Round(Math.Max(lo, Math.Min(hi, db)), 1);
    }

    /// <summary>
    /// Recalls the preset <paramref name="key"/> (matched case-insensitively, trimmed) from presets built by
    /// <see cref="Normalize"/>: sends its level through <paramref name="applyLevel"/> and its mute through
    /// <paramref name="applyMute"/>, only for the fields present. A mute is sent before the level and an unmute
    /// after it, so the level change itself isn't heard. Returns false (and applies nothing) when the key is unknown
    /// </summary>
    public static bool Recall(IDictionary<string, GenelecSmartIpPresetConfig> presets, string key,
        double minDb, double maxDb, Action<double> applyLevel, Action<bool> applyMute)
    {
      GenelecSmartIpPresetConfig preset;
      if (presets == null || key == null || !presets.TryGetValue(key.Trim(), out preset) || preset == null)
        return false;

      var level = Level(preset, minDb, maxDb);
      var mute = preset.Mute;

      if (mute == true) applyMute(true);
      if (level.HasValue) applyLevel(level.Value);
      if (mute == false) applyMute(false);
      return true;
    }
  }
}
