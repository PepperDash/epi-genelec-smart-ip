using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PepperDash.Essentials.Plugins.Tests;

public class FactoryTests
{
  private const string FactoryName = "GenelecSmartIpDeviceFactory";
  private const string MulticastFactoryName = "GenelecSmartIpMulticastGroupFactory";

  [Fact]
  public void Assembly_Loads_And_Has_Expected_Name()
  {
    AssemblyFixture.PluginAssembly.GetName().Name
        .Should().Be("PepperDash.Essentials.Plugins.Genelec.Smart.Ip");
  }

  [Fact]
  public void Both_Factories_Are_Discoverable()
  {
    // direct speaker control and multicast group control: the EPI deploys either way
    var factories = AssemblyFixture.FindFactoryTypes().Select(t => t.Name).ToList();
    factories.Should().BeEquivalentTo(new[] { FactoryName, MulticastFactoryName });
  }

  [Fact]
  public void Factories_Have_Parameterless_Constructors()
  {
    foreach (var factory in AssemblyFixture.FindFactoryTypes())
      factory.GetConstructor(Type.EmptyTypes).Should().NotBeNull(factory.Name);
  }

  [Theory]
  [InlineData(FactoryName, "genelecSmartIp")]
  [InlineData(MulticastFactoryName, "genelecSmartIpMulticastGroup")]
  public void Factory_Registers_Its_TypeName_Once(string factory, string typeName)
  {
    var src = AssemblyFixture.FindSourceForClass(factory);
    src.Should().NotBeNull();
    src!.Should().Contain("TypeNames");
    Regex.Matches(src!, "\"" + typeName + "\"").Count.Should().Be(1);
  }

  [Theory]
  [InlineData(FactoryName)]
  [InlineData(MulticastFactoryName)]
  public void Minimum_Version_Is_Numeric_And_Matches_Csproj_Core(string factory)
  {
    var src = AssemblyFixture.FindSourceForClass(factory)!;
    var min = Regex.Match(src, "MinimumEssentialsFrameworkVersion\\s*=\\s*\"([^\"]*)\"").Groups[1].Value;
    min.Should().MatchRegex("^\\d+\\.\\d+\\.\\d+$", "a prerelease suffix makes the loader skip the plugin");

    var csproj = File.ReadAllText(Path.Combine(AssemblyFixture.SourceDirectory, "epi-genelec-smart-ip.4Series.csproj"));
    var pkg = Regex.Match(csproj, "Include=\"PepperDashEssentials\"\\s+Version=\"(\\d+\\.\\d+\\.\\d+)").Groups[1].Value;
    min.Should().Be(pkg);
  }
}
