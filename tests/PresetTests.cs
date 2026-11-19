using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace PepperDash.Essentials.Plugins.Tests;

public class PresetTests
{
  private const string GroupJson = @"{
    ""volumeGroup"": { ""address"": ""239.10.10.1"", ""port"": 49152, ""members"": [] },
    ""volumeMinDb"": -60, ""volumeMaxDb"": -10,
    ""presets"": {
      ""startup"":  { ""name"": ""Startup"", ""levelDb"": -25, ""mute"": false },
      ""shutdown"": { ""name"": ""Shutdown"", ""mute"": true },
      ""loud"":     { ""levelDb"": 6 },
      ""quiet"":    { ""levelDb"": -90.04 },
      ""duck"":     { ""levelDb"": -40, ""mute"": true }
    }
  }";

  /// <summary>Records what a recall sent, in order, through fake level and mute paths</summary>
  private sealed class Recorder
  {
    public readonly List<string> Calls = new();
    public void Level(double db) => Calls.Add("level " + db.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
    public void Mute(bool mute) => Calls.Add("mute " + mute.ToString().ToLowerInvariant());
  }

  private static GenelecSmartIpMulticastGroupConfig GroupConfig() =>
      JsonConvert.DeserializeObject<GenelecSmartIpMulticastGroupConfig>(GroupJson)!;

  private static Dictionary<string, GenelecSmartIpPresetConfig> Presets() =>
      GenelecSmartIpPresets.Normalize(GroupConfig().Presets);

  private static List<string> RecallFromGroupConfig(string key, out bool found)
  {
    var cfg = GroupConfig();
    var rec = new Recorder();
    found = GenelecSmartIpPresets.Recall(GenelecSmartIpPresets.Normalize(cfg.Presets), key,
        cfg.VolumeMinDb, cfg.VolumeMaxDb, rec.Level, rec.Mute);
    return rec.Calls;
  }

  // ---- config binding ------------------------------------------------------------------------------

  [Fact]
  public void Group_Config_Binds_Presets()
  {
    var p = GroupConfig().Presets;
    p.Should().ContainKeys("startup", "shutdown");
    p["startup"].Name.Should().Be("Startup");
    p["startup"].LevelDb.Should().Be(-25);
    p["startup"].Mute.Should().BeFalse();
    p["shutdown"].LevelDb.Should().BeNull();   // absent fields stay null: not applied
    p["shutdown"].Mute.Should().BeTrue();
    p["loud"].Name.Should().BeNull();
    p["loud"].Mute.Should().BeNull();
  }

  [Fact]
  public void Group_Config_Without_Presets_Is_Unchanged()
  {
    var cfg = JsonConvert.DeserializeObject<GenelecSmartIpMulticastGroupConfig>(
        @"{ ""volumeGroup"": { ""address"": ""239.10.10.1"", ""port"": 49152 } }")!;
    cfg.Presets.Should().BeNull();
    GenelecSmartIpPresets.Normalize(cfg.Presets).Should().BeEmpty();
  }

  [Theory]
  [InlineData("GenelecSmartIpPropertiesConfig")]
  [InlineData("GenelecSmartIpMulticastGroupConfig")]
  public void Both_Device_Configs_Have_An_Optional_Presets_Object(string type)
  {
    var t = AssemblyFixture.PluginAssembly.GetType("PepperDash.Essentials.Plugins." + type)!;
    var prop = t.GetProperty("Presets")!;
    prop.Should().NotBeNull();
    prop.CustomAttributes.Should().Contain(a => a.AttributeType.Name == "JsonPropertyAttribute"
        && a.ConstructorArguments.Any(arg => (string?)arg.Value == "presets"));
    prop.PropertyType.GetGenericArguments().Select(a => a.Name)
        .Should().Equal("String", "GenelecSmartIpPresetConfig");
    AssemblyFixture.FindSourceForClass(type)!.Should().NotContain("Presets { get; set; } =");   // no default: optional
  }

  [Fact]
  public void Display_Name_Falls_Back_To_The_Key()
  {
    var p = Presets();
    GenelecSmartIpPresets.DisplayName("startup", p["startup"]).Should().Be("Startup");
    GenelecSmartIpPresets.DisplayName("loud", p["loud"]).Should().Be("loud");
  }

  [Fact]
  public void Normalize_Skips_Blank_Null_And_Case_Duplicate_Keys_With_A_Warning()
  {
    var warnings = new List<string>();
    var raw = new Dictionary<string, GenelecSmartIpPresetConfig>
    {
      { " Startup ", new GenelecSmartIpPresetConfig { LevelDb = -20 } },
      { "STARTUP", new GenelecSmartIpPresetConfig { LevelDb = -30 } },
      { "  ", new GenelecSmartIpPresetConfig { Mute = true } },
      { "empty", null! },
      { "nothing", new GenelecSmartIpPresetConfig { Name = "Nothing" } },
    };

    var p = GenelecSmartIpPresets.Normalize(raw, warnings.Add);

    p.Keys.Should().BeEquivalentTo("Startup", "nothing");
    p["startup"].LevelDb.Should().Be(-20);   // first wins
    warnings.Should().HaveCount(4);
  }

  // ---- recall ---------------------------------------------------------------------------------------

  [Fact]
  public void Level_Only_Preset_Sends_Only_The_Level()
  {
    var rec = new Recorder();
    var p = new Dictionary<string, GenelecSmartIpPresetConfig> { { "mid", new GenelecSmartIpPresetConfig { LevelDb = -30 } } };
    GenelecSmartIpPresets.Recall(GenelecSmartIpPresets.Normalize(p), "mid", -60, 0, rec.Level, rec.Mute).Should().BeTrue();
    rec.Calls.Should().Equal("level -30.0");
  }

  [Fact]
  public void Mute_Only_Preset_Sends_Only_The_Mute()
  {
    RecallFromGroupConfig("shutdown", out var found).Should().Equal("mute true");
    found.Should().BeTrue();
  }

  [Fact]
  public void Level_And_Unmute_Sends_Level_Then_Unmute()
  {
    RecallFromGroupConfig("startup", out _).Should().Equal("level -25.0", "mute false");
  }

  [Fact]
  public void Level_And_Mute_Sends_Mute_Then_Level()
  {
    RecallFromGroupConfig("duck", out _).Should().Equal("mute true", "level -40.0");
  }

  [Theory]
  [InlineData("loud", "level -10.0")]    // +6 dB clamped to volumeMaxDb -10
  [InlineData("quiet", "level -60.0")]   // -90 dB clamped to volumeMinDb -60
  public void Level_Is_Clamped_To_The_Device_Range(string key, string expected)
  {
    RecallFromGroupConfig(key, out _).Should().Equal(expected);
  }

  [Fact]
  public void Level_Is_Rounded_To_A_Tenth_And_Clamp_Accepts_A_Reversed_Range()
  {
    var p = new GenelecSmartIpPresetConfig { LevelDb = -12.34 };
    GenelecSmartIpPresets.Level(p, -60, 0).Should().Be(-12.3);
    GenelecSmartIpPresets.Level(new GenelecSmartIpPresetConfig { LevelDb = -80 }, 0, -60).Should().Be(-60);
    GenelecSmartIpPresets.Level(new GenelecSmartIpPresetConfig { LevelDb = double.NaN }, -60, 0).Should().BeNull();
    GenelecSmartIpPresets.Level(new GenelecSmartIpPresetConfig { Mute = true }, -60, 0).Should().BeNull();
  }

  [Theory]
  [InlineData("STARTUP")]
  [InlineData("StartUp")]
  [InlineData(" startup ")]
  public void Keys_Match_Case_Insensitively(string key)
  {
    RecallFromGroupConfig(key, out var found).Should().Equal("level -25.0", "mute false");
    found.Should().BeTrue();
  }

  [Theory]
  [InlineData("nope")]
  [InlineData("")]
  [InlineData(null)]
  public void Unknown_Key_Does_Nothing(string? key)
  {
    RecallFromGroupConfig(key!, out var found).Should().BeEmpty();
    found.Should().BeFalse();
  }

  [Fact]
  public void No_Presets_Configured_Recalls_Nothing()
  {
    var rec = new Recorder();
    GenelecSmartIpPresets.Recall(GenelecSmartIpPresets.Normalize(null!), "startup", -60, 0, rec.Level, rec.Mute).Should().BeFalse();
    rec.Calls.Should().BeEmpty();
  }

  // ---- device surface ----------------------------------------------------------------------------

  [Theory]
  [InlineData("GenelecSmartIpMulticastGroup")]
  [InlineData("GenelecSmartIpDevice")]
  public void Device_Implements_IDspPresets(string type)
  {
    var t = AssemblyFixture.PluginAssembly.GetType("PepperDash.Essentials.Plugins." + type)!;
    t.GetInterfaces().Select(i => i.FullName).Should().Contain("PepperDash.Essentials.Core.IDspPresets");
    t.GetMethod("RecallPreset", new[] { AssemblyFixture.Context.CoreAssembly!.GetType("System.String")! }).Should().NotBeNull();
    t.GetProperty("Presets")!.PropertyType.GetGenericArguments().Select(a => a.Name).Should().Equal("String", "IKeyName");
  }

  [Theory]
  [InlineData("GenelecSmartIpMulticastGroup", "ApplyLevelLocked(db)", "SetMuteLocked")]
  [InlineData("GenelecSmartIpDevice", "SetLevelDb(db)", "SetMute")]
  public void Recall_Uses_The_Devices_Normal_Level_And_Mute_Paths(string type, string levelPath, string mutePath)
  {
    var src = AssemblyFixture.FindSourceForClass(type)!;
    // RecallPreset sits just above SetMute in both classes
    var start = src.IndexOf("public void RecallPreset(string key)", StringComparison.Ordinal);
    start.Should().BeGreaterThan(0);
    var end = src.IndexOf("private void SetMute(bool mute)", start, StringComparison.Ordinal);
    end.Should().BeGreaterThan(start);
    var recall = src[start..end];
    recall.Should().Contain("GenelecSmartIpPresets.Recall(").And.Contain(levelPath).And.Contain(mutePath);
    recall.Should().Contain("LogWarning");   // unknown key
  }
}
