using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PepperDash.Essentials.Plugins.Tests;

public class BridgeTests
{
  private const string DeviceName = "GenelecSmartIpDevice";
  private const string JoinMapName = "GenelecSmartIpBridgeJoinMap";

  private static Type DeviceType =>
      AssemblyFixture.PluginAssembly.GetTypes().Single(t => t.Name == DeviceName);

  [Theory]
  [InlineData("IBridgeAdvanced")]
  [InlineData("IDeviceInfoProvider")]
  [InlineData("IBasicVolumeWithFeedback")]
  [InlineData("IHasPowerControlWithFeedback")]
  [InlineData("ICommunicationMonitor")]
  public void Device_Implements_Interface(string interfaceName)
  {
    DeviceType.GetInterfaces().Select(i => i.Name).Should().Contain(interfaceName);
  }

  [Fact]
  public void JoinMap_Derives_From_JoinMapBaseAdvanced()
  {
    var type = AssemblyFixture.PluginAssembly.GetTypes().Single(t => t.Name == JoinMapName);
    type.BaseType!.Name.Should().Be("JoinMapBaseAdvanced");
  }

  [Fact]
  public void JoinMap_Has_No_Same_Direction_Collisions()
  {
    var joins = ParseJoins();
    var declared = Regex.Matches(AssemblyFixture.FindSourceForClass(JoinMapName)!, @"public JoinDataComplete \w+").Count;
    joins.Should().HaveCount(declared, "every join must be parsed for the collision check to mean anything");

    foreach (var direction in new[] { "To", "From" })
    {
      var used = new Dictionary<(string type, uint join), string>();
      foreach (var j in joins.Where(j => j.Capabilities.Contains(direction + "SIMPL") || j.Capabilities == "ToFromSIMPL"))
      {
        for (var n = j.Number; n < j.Number + j.Span; n++)
        {
          used.Should().NotContainKey((j.Type, n),
              $"{j.Name} ({j.Type} {n}, {direction}SIMPL) collides with another join");
          used[(j.Type, n)] = j.Name;
        }
      }
    }
  }

  private record Join(string Name, uint Number, uint Span, string Capabilities, string Type);

  private static List<Join> ParseJoins()
  {
    var src = AssemblyFixture.FindSourceForClass(JoinMapName)!;
    var spanConst = uint.Parse(Regex.Match(src, @"MaxInputs\s*=\s*(\d+)").Groups[1].Value);

    var pattern = new Regex(
        @"public JoinDataComplete (?<name>\w+)\s*=.*?JoinNumber\s*=\s*(?<num>\d+),\s*JoinSpan\s*=\s*(?<span>\w+).*?eJoinCapabilities\.(?<cap>\w+).*?eJoinType\.(?<type>\w+)",
        RegexOptions.Singleline);

    return pattern.Matches(src).Select(m => new Join(
        m.Groups["name"].Value,
        uint.Parse(m.Groups["num"].Value),
        m.Groups["span"].Value == "MaxInputs" ? spanConst : uint.Parse(m.Groups["span"].Value),
        m.Groups["cap"].Value,
        m.Groups["type"].Value)).ToList();
  }
}
