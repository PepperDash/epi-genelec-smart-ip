using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PepperDash.Essentials.Plugins.Tests;

public class FactoryTests
{
  private const string FactoryName = "GenelecSmartIpDeviceFactory";

  [Fact]
  public void Assembly_Loads_And_Has_Expected_Name()
  {
    AssemblyFixture.PluginAssembly.GetName().Name
        .Should().Be("PepperDash.Essentials.Plugins.Genelec.SmartIp");
  }

  [Fact]
  public void Exactly_One_Factory_Is_Discoverable()
  {
    var factories = AssemblyFixture.FindFactoryTypes();
    factories.Should().ContainSingle().Which.Name.Should().Be(FactoryName);
  }

  [Fact]
  public void Factory_Has_Parameterless_Constructor()
  {
    AssemblyFixture.FindFactoryTypes().Single().GetConstructor(Type.EmptyTypes).Should().NotBeNull();
  }

  [Fact]
  public void Factory_Registers_TypeName_genelecSmartIp()
  {
    var src = AssemblyFixture.FindSourceForClass(FactoryName);
    src.Should().NotBeNull();
    src!.Should().Contain("TypeNames");
    Regex.Matches(src!, "\"genelecSmartIp\"").Count.Should().Be(1);
  }

  [Fact]
  public void Minimum_Version_Is_Numeric_And_Matches_Csproj_Core()
  {
    var src = AssemblyFixture.FindSourceForClass(FactoryName)!;
    var min = Regex.Match(src, "MinimumEssentialsFrameworkVersion\\s*=\\s*\"([^\"]*)\"").Groups[1].Value;
    min.Should().MatchRegex("^\\d+\\.\\d+\\.\\d+$", "a prerelease suffix makes the loader skip the plugin");

    var csproj = File.ReadAllText(Path.Combine(AssemblyFixture.SourceDirectory, "epi-genelec-smart-ip.4Series.csproj"));
    var pkg = Regex.Match(csproj, "Include=\"PepperDashEssentials\"\\s+Version=\"(\\d+\\.\\d+\\.\\d+)").Groups[1].Value;
    min.Should().Be(pkg);
  }
}
