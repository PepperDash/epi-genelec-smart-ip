using System.Collections.Generic;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;

namespace PepperDash.Essentials.Plugins
{
  /// <summary>
  /// Factory for the Genelec Smart IP device
  /// </summary>
  public class GenelecSmartIpDeviceFactory : EssentialsPluginDeviceFactory<GenelecSmartIpDevice>
  {
    /// <summary>
    /// Registers the device type name(s)
    /// </summary>
    public GenelecSmartIpDeviceFactory()
    {
      // Keep equal to the PepperDashEssentials PackageReference version in the csproj: the version
      // this plugin is compiled against is its minimum
      MinimumEssentialsFrameworkVersion = "2.39.0";

      TypeNames = new List<string> { "genelecSmartIp" };
    }

    /// <inheritdoc />
    public override EssentialsDevice BuildDevice(DeviceConfig dc)
    {
      Serilog.Log.Debug("[{Key}] Factory attempting to create new device from type: {Type}", dc.Key, dc.Type);

      var config = dc.Properties.ToObject<GenelecSmartIpPropertiesConfig>();
      if (config == null)
      {
        Serilog.Log.Error("[{Key}] Factory: failed to read properties config for {Name}", dc.Key, dc.Name);
        return null;
      }

      if (string.IsNullOrWhiteSpace(config.Host))
      {
        Serilog.Log.Error("[{Key}] Factory: \"host\" is required in properties for {Name}", dc.Key, dc.Name);
        return null;
      }

      return new GenelecSmartIpDevice(dc.Key, dc.Name, config);
    }
  }
}
