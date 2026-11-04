using System.Collections.Generic;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;

namespace PepperDash.Essentials.Plugins
{
  /// <summary>
  /// Factory for the Genelec Smart IP multicast group device
  /// </summary>
  public class GenelecSmartIpMulticastGroupFactory : EssentialsPluginDeviceFactory<GenelecSmartIpMulticastGroup>
  {
    /// <summary>
    /// Registers the device type name(s)
    /// </summary>
    public GenelecSmartIpMulticastGroupFactory()
    {
      // Keep equal to the PepperDashEssentials PackageReference version in the csproj
      MinimumEssentialsFrameworkVersion = "2.39.0";

      TypeNames = new List<string> { "genelecSmartIpMulticastGroup" };
    }

    /// <inheritdoc />
    public override EssentialsDevice BuildDevice(DeviceConfig dc)
    {
      Serilog.Log.Debug("[{Key}] Factory attempting to create new device from type: {Type}", dc.Key, dc.Type);

      var config = dc.Properties != null ? dc.Properties.ToObject<GenelecSmartIpMulticastGroupConfig>() : null;
      if (config == null || config.VolumeGroup == null)
      {
        Serilog.Log.Error("[{Key}] Factory: \"volumeGroup\" (address, port) is required in properties for {Name}", dc.Key, dc.Name);
        return null;
      }

      var problem = GenelecSmartIpMulticast.ValidateGroup(config.VolumeGroup.Address, config.VolumeGroup.Port);
      if (problem != null)
      {
        Serilog.Log.Error("[{Key}] Factory: volumeGroup {Problem}", dc.Key, problem);
        return null;
      }

      if (config.MuteGroup != null)
      {
        problem = GenelecSmartIpMulticast.ValidateGroup(config.MuteGroup.Address, config.MuteGroup.Port);
        if (problem != null)
        {
          Serilog.Log.Error("[{Key}] Factory: muteGroup {Problem}", dc.Key, problem);
          return null;
        }
      }

      return new GenelecSmartIpMulticastGroup(dc.Key, dc.Name, config);
    }
  }
}
