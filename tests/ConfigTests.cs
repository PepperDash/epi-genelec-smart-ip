using FluentAssertions;
using Xunit;

namespace PepperDash.Essentials.Plugins.Tests;

public class ConfigTests
{
  private static Type ConfigType =>
      AssemblyFixture.PluginAssembly.GetType("PepperDash.Essentials.Plugins.GenelecSmartIpPropertiesConfig")!;

  [Fact]
  public void Config_Class_Exists_With_Parameterless_Ctor()
  {
    ConfigType.Should().NotBeNull();
    ConfigType.GetConstructor(Type.EmptyTypes).Should().NotBeNull();
  }

  [Theory]
  [InlineData("host")]
  [InlineData("port")]
  [InlineData("username")]
  [InlineData("password")]
  [InlineData("pollTimeMs")]
  [InlineData("requestTimeoutMs")]
  [InlineData("volumeMinDb")]
  [InlineData("volumeMaxDb")]
  [InlineData("volumeStepDb")]
  [InlineData("volumeRampIntervalMs")]
  [InlineData("inputs")]
  [InlineData("fixedLevelDb")]
  public void Config_Property_Has_JsonPropertyAttribute(string jsonName)
  {
    var has = ConfigType.GetProperties().Any(p => p.CustomAttributes.Any(a =>
        a.AttributeType.Name == "JsonPropertyAttribute"
        && a.ConstructorArguments.Any(arg => string.Equals(arg.Value?.ToString(), jsonName, StringComparison.Ordinal))));
    has.Should().BeTrue();
  }

  [Fact]
  public void Input_Config_Class_Has_Name_And_Inputs()
  {
    var t = AssemblyFixture.PluginAssembly.GetType("PepperDash.Essentials.Plugins.GenelecSmartIpInputConfig")!;
    t.GetProperties().Select(p => p.Name).Should().Contain(new[] { "Name", "Inputs" });
  }

  [Fact]
  public void Defaults_Are_Documented_In_Source()
  {
    // MetadataLoadContext cannot execute initializers, so check the source text
    var src = AssemblyFixture.FindSourceForClass("GenelecSmartIpPropertiesConfig")!;
    src.Should().Contain("Port { get; set; } = 9000");
    src.Should().Contain("Username { get; set; } = \"admin\"");
    src.Should().Contain("Password { get; set; } = \"admin\"");
    src.Should().Contain("PollTimeMs { get; set; } = 5000");
    src.Should().Contain("VolumeMinDb { get; set; } = -130");
    src.Should().Contain("VolumeMaxDb { get; set; } = 0");
  }

  [Fact]
  public void FixedLevelDb_Is_Optional()
  {
    // nullable, so an existing config without it deserializes unchanged (feature off)
    var prop = ConfigType.GetProperty("FixedLevelDb")!;
    prop.Should().NotBeNull();
    prop.PropertyType.IsGenericType.Should().BeTrue();
    prop.PropertyType.GetGenericTypeDefinition().FullName.Should().Be("System.Nullable`1");
    prop.PropertyType.GetGenericArguments()[0].FullName.Should().Be("System.Double");

    // no default value and never required
    var src = AssemblyFixture.FindSourceForClass("GenelecSmartIpPropertiesConfig")!;
    src.Should().Contain("public double? FixedLevelDb { get; set; }").And.NotContain("FixedLevelDb { get; set; } =");
    src.Should().NotContain("Required.Always");
  }

  [Fact]
  public void Default_Inputs_Are_The_Four_Agreed_Items()
  {
    var src = AssemblyFixture.FindSourceForClass("GenelecSmartIpPropertiesConfig")!;
    foreach (var k in new[] { "\"analog\"", "\"aoip1\"", "\"aoip2\"", "\"aoip1and2\"" })
      src.Should().Contain(k);
  }
}
