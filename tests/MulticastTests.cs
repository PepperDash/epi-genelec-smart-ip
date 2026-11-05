using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using Xunit;

namespace PepperDash.Essentials.Plugins.Tests;

public class MulticastTests
{
  private static string Text(byte[] b) => Encoding.UTF8.GetString(b);

  // ---- messages (API doc section 6) ------------------------------------------------------------

  [Theory]
  [InlineData(-30.0, "{\"mcast\":{\"ver\":1,\"level\":-30.0}}")]
  [InlineData(-12.34, "{\"mcast\":{\"ver\":1,\"level\":-12.3}}")]     // 0.1 dB resolution
  [InlineData(-150.0, "{\"mcast\":{\"ver\":1,\"level\":-130.0}}")]    // multicast range is -130..0
  [InlineData(3.0, "{\"mcast\":{\"ver\":1,\"level\":0.0}}")]
  public void Level_Message_Is_Clamped_And_Rounded(double db, string expected)
  {
    Text(GenelecSmartIpMulticast.LevelMessage(db)).Should().Be(expected);
  }

  [Fact]
  public void Level_Message_Uses_A_Dot_Whatever_The_Culture()
  {
    var previous = CultureInfo.CurrentCulture;
    try
    {
      CultureInfo.CurrentCulture = new CultureInfo("de-DE");   // decimal comma
      Text(GenelecSmartIpMulticast.LevelMessage(-20.5)).Should().Be("{\"mcast\":{\"ver\":1,\"level\":-20.5}}");
    }
    finally
    {
      CultureInfo.CurrentCulture = previous;
    }
  }

  [Fact]
  public void Mute_Messages()
  {
    Text(GenelecSmartIpMulticast.MuteMessage(true)).Should().Be("{\"mcast\":{\"ver\":1,\"mute\":true}}");
    Text(GenelecSmartIpMulticast.MuteMessage(false)).Should().Be("{\"mcast\":{\"ver\":1,\"mute\":false}}");
  }

  [Fact]
  public void Power_Messages_Are_Standby_And_Boot_Only()
  {
    Text(GenelecSmartIpMulticast.PowerMessage("STANDBY")).Should().Be("{\"mcast\":{\"ver\":1,\"state\":\"STANDBY\"}}");
    Text(GenelecSmartIpMulticast.PowerMessage("BOOT")).Should().Be("{\"mcast\":{\"ver\":1,\"state\":\"BOOT\"}}");
    var act = () => GenelecSmartIpMulticast.PowerMessage("ACTIVE");   // not a multicast state
    act.Should().Throw<ArgumentException>();
  }

  [Fact]
  public void ClampDb_Handles_NaN()
  {
    GenelecSmartIpMulticast.ClampDb(double.NaN).Should().Be(-130.0);
  }

  [Theory]
  [InlineData(-20.0, -20.04, false)]
  [InlineData(-20.0, -20.1, true)]
  [InlineData(-60.0, -12.0, true)]
  public void LevelDiffers_At_Device_Resolution(double a, double b, bool expected)
  {
    GenelecSmartIpMulticast.LevelDiffers(a, b).Should().Be(expected);
  }

  // ---- validation --------------------------------------------------------------------------------

  [Theory]
  [InlineData("239.10.10.1", 49152)]
  [InlineData("224.0.1.5", 1024)]
  [InlineData(" 239.255.255.255 ", 65535)]
  public void Valid_Groups(string address, int port)
  {
    GenelecSmartIpMulticast.ValidateGroup(address, port).Should().BeNull();
  }

  [Theory]
  [InlineData("192.168.1.10", 49152, "not multicast")]
  [InlineData("240.0.0.1", 49152, "not multicast")]
  [InlineData("not-an-ip", 49152, "IPv4")]
  [InlineData("", 49152, "IPv4")]
  [InlineData("ff02::1", 49152, "IPv4")]
  [InlineData("239.10.10.1", 80, "1024-65535")]
  [InlineData("239.10.10.1", 70000, "1024-65535")]
  public void Invalid_Groups(string address, int port, string reason)
  {
    GenelecSmartIpMulticast.ValidateGroup(address, port).Should().Contain(reason);
  }

  [Fact]
  public void Speaker_Group_Comparison()
  {
    GenelecSmartIpMulticast.CompareSpeakerGroup("239.10.10.1", 49152, "239.10.10.1", 49152).Should().BeNull();
    GenelecSmartIpMulticast.CompareSpeakerGroup("239.10.10.2", 49152, "239.10.10.1", 49152).Should().Contain("listens on 239.10.10.2:49152");
    GenelecSmartIpMulticast.CompareSpeakerGroup("239.10.10.1", 49153, "239.10.10.1", 49152).Should().Contain(":49153");
    GenelecSmartIpMulticast.CompareSpeakerGroup("0.0.0.0", 49152, "239.10.10.1", 49152).Should().Contain("disabled");
    GenelecSmartIpMulticast.CompareSpeakerGroup(null, null, "239.10.10.1", 49152).Should().Contain("did not report");
  }

  // ---- sender (real UDP, to a local listener) --------------------------------------------------

  [Fact]
  public void Sender_Sends_Each_Message_Count_Times()
  {
    using var listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
    listener.Client.ReceiveTimeout = 2000;
    var target = (IPEndPoint)listener.Client.LocalEndPoint!;

    using var sender = new GenelecSmartIpMulticastSender("127.0.0.1");
    var payload = GenelecSmartIpMulticast.MuteMessage(true);
    sender.Send(target, payload, count: 3, gapMs: 5).Should().BeNull();

    var from = new IPEndPoint(IPAddress.Any, 0);
    for (var i = 0; i < 3; i++)
      listener.Receive(ref from).Should().Equal(payload);
  }

  [Fact]
  public void Sender_Rejects_A_Bad_Local_Address()
  {
    var act = () => new GenelecSmartIpMulticastSender("not-an-ip");
    act.Should().Throw<ArgumentException>();
  }

  // ---- config and device surface (metadata) -----------------------------------------------------

  private static Type PluginType(string name) =>
      AssemblyFixture.PluginAssembly.GetType("PepperDash.Essentials.Plugins." + name)!;

  [Theory]
  [InlineData("GenelecSmartIpMulticastGroupConfig", "volumeGroup")]
  [InlineData("GenelecSmartIpMulticastGroupConfig", "muteGroup")]
  [InlineData("GenelecSmartIpMulticastGroupConfig", "volumeMinDb")]
  [InlineData("GenelecSmartIpMulticastGroupConfig", "volumeMaxDb")]
  [InlineData("GenelecSmartIpMulticastGroupConfig", "volumeStepDb")]
  [InlineData("GenelecSmartIpMulticastGroupConfig", "volumeRampIntervalMs")]
  [InlineData("GenelecSmartIpMulticastGroupConfig", "localAddress")]
  [InlineData("GenelecSmartIpMulticastGroupConfig", "sendCount")]
  [InlineData("GenelecSmartIpMulticastGroupConfig", "repairDelayMs")]
  [InlineData("GenelecSmartIpMulticastGroupTarget", "address")]
  [InlineData("GenelecSmartIpMulticastGroupTarget", "port")]
  [InlineData("GenelecSmartIpMulticastGroupTarget", "members")]
  public void Config_Property_Has_JsonPropertyAttribute(string type, string jsonName)
  {
    PluginType(type).GetProperties().Any(p => p.CustomAttributes.Any(a =>
        a.AttributeType.Name == "JsonPropertyAttribute"
        && a.ConstructorArguments.Any(arg => string.Equals(arg.Value?.ToString(), jsonName, StringComparison.Ordinal))))
        .Should().BeTrue();
  }

  [Fact]
  public void Config_Defaults_Are_Documented_In_Source()
  {
    var src = AssemblyFixture.FindSourceForClass("GenelecSmartIpMulticastGroupConfig")!;
    src.Should().Contain("VolumeMinDb { get; set; } = -60");
    src.Should().Contain("VolumeMaxDb { get; set; } = 0");
    src.Should().Contain("SendCount { get; set; } = 2");
    src.Should().Contain("RepairDelayMs { get; set; } = 6000");
  }

  [Theory]
  [InlineData("IBasicVolumeWithFeedback")]
  [InlineData("IHasPowerControlWithFeedback")]
  public void Group_Implements_Interface(string name)
  {
    PluginType("GenelecSmartIpMulticastGroup").GetInterfaces().Select(i => i.Name).Should().Contain(name);
  }

  [Theory]
  [InlineData("LevelDb")]
  [InlineData("HasFixedLevel")]
  [InlineData("MulticastAddress")]
  [InlineData("MulticastPort")]
  public void Speaker_Exposes_What_The_Group_Reads(string property)
  {
    PluginType("GenelecSmartIpDevice").GetProperty(property).Should().NotBeNull();
  }

  [Fact]
  public void Speaker_Raises_VolumeReported_For_The_Group_To_Learn_From()
  {
    PluginType("GenelecSmartIpDevice").GetEvent("VolumeReported").Should().NotBeNull();
    var args = PluginType("GenelecSmartIpVolumeReportEventArgs");
    args.GetProperty("LevelReported").Should().NotBeNull();
    args.GetProperty("MuteReported").Should().NotBeNull();
  }
}
