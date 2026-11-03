using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.Plugins
{
  /// <summary>
  /// EISC bridge join map for the Genelec Smart IP loudspeaker
  /// </summary>
  public class GenelecSmartIpBridgeJoinMap : JoinMapBaseAdvanced
  {
    /// <summary>
    /// Maximum number of selectable inputs exposed on the bridge
    /// </summary>
    public const uint MaxInputs = 10;

    #region Digital

    /// <summary>
    /// Device online feedback
    /// </summary>
    [JoinName("IsOnline")]
    public JoinDataComplete IsOnline = new JoinDataComplete(
      new JoinData { JoinNumber = 1, JoinSpan = 1 },
      new JoinMetadata
      {
        Description = "Is Online",
        JoinCapabilities = eJoinCapabilities.ToSIMPL,
        JoinType = eJoinType.Digital
      });

    /// <summary>
    /// Power on (ACTIVE) and power on feedback
    /// </summary>
    [JoinName("PowerOn")]
    public JoinDataComplete PowerOn = new JoinDataComplete(
      new JoinData { JoinNumber = 2, JoinSpan = 1 },
      new JoinMetadata
      {
        Description = "Power On (ACTIVE) & Power Is On feedback",
        JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
        JoinType = eJoinType.Digital
      });

    /// <summary>
    /// Power off (STANDBY) and power off feedback
    /// </summary>
    [JoinName("PowerOff")]
    public JoinDataComplete PowerOff = new JoinDataComplete(
      new JoinData { JoinNumber = 3, JoinSpan = 1 },
      new JoinMetadata
      {
        Description = "Power Off (STANDBY) & Power Is Off feedback",
        JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
        JoinType = eJoinType.Digital
      });

    /// <summary>
    /// Power toggle
    /// </summary>
    [JoinName("PowerToggle")]
    public JoinDataComplete PowerToggle = new JoinDataComplete(
      new JoinData { JoinNumber = 4, JoinSpan = 1 },
      new JoinMetadata
      {
        Description = "Power Toggle",
        JoinCapabilities = eJoinCapabilities.FromSIMPL,
        JoinType = eJoinType.Digital
      });

    /// <summary>
    /// Volume up (ramps while held)
    /// </summary>
    [JoinName("VolumeUp")]
    public JoinDataComplete VolumeUp = new JoinDataComplete(
      new JoinData { JoinNumber = 5, JoinSpan = 1 },
      new JoinMetadata
      {
        Description = "Volume Up (ramps while held)",
        JoinCapabilities = eJoinCapabilities.FromSIMPL,
        JoinType = eJoinType.Digital
      });

    /// <summary>
    /// Volume down (ramps while held)
    /// </summary>
    [JoinName("VolumeDown")]
    public JoinDataComplete VolumeDown = new JoinDataComplete(
      new JoinData { JoinNumber = 6, JoinSpan = 1 },
      new JoinMetadata
      {
        Description = "Volume Down (ramps while held)",
        JoinCapabilities = eJoinCapabilities.FromSIMPL,
        JoinType = eJoinType.Digital
      });

    /// <summary>
    /// Mute toggle and mute on feedback
    /// </summary>
    [JoinName("MuteToggle")]
    public JoinDataComplete MuteToggle = new JoinDataComplete(
      new JoinData { JoinNumber = 7, JoinSpan = 1 },
      new JoinMetadata
      {
        Description = "Mute Toggle & Mute Is On feedback",
        JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
        JoinType = eJoinType.Digital
      });

    /// <summary>
    /// Mute on and mute on feedback
    /// </summary>
    [JoinName("MuteOn")]
    public JoinDataComplete MuteOn = new JoinDataComplete(
      new JoinData { JoinNumber = 8, JoinSpan = 1 },
      new JoinMetadata
      {
        Description = "Mute On & Mute Is On feedback",
        JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
        JoinType = eJoinType.Digital
      });

    /// <summary>
    /// Mute off and mute off feedback
    /// </summary>
    [JoinName("MuteOff")]
    public JoinDataComplete MuteOff = new JoinDataComplete(
      new JoinData { JoinNumber = 9, JoinSpan = 1 },
      new JoinMetadata
      {
        Description = "Mute Off & Mute Is Off feedback",
        JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
        JoinType = eJoinType.Digital
      });

    /// <summary>
    /// Input select by position (config order) and selected feedback
    /// </summary>
    [JoinName("InputSelect")]
    public JoinDataComplete InputSelect = new JoinDataComplete(
      new JoinData { JoinNumber = 11, JoinSpan = MaxInputs },
      new JoinMetadata
      {
        Description = "Input Select (config order) & Input Is Selected feedback",
        JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
        JoinType = eJoinType.Digital
      });

    #endregion

    #region Analog

    /// <summary>
    /// Volume level (0-65535 over volumeMinDb..volumeMaxDb) and feedback
    /// </summary>
    [JoinName("Volume")]
    public JoinDataComplete Volume = new JoinDataComplete(
      new JoinData { JoinNumber = 1, JoinSpan = 1 },
      new JoinMetadata
      {
        Description = "Volume Level set & feedback (0-65535 scaled over volumeMinDb..volumeMaxDb)",
        JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
        JoinType = eJoinType.Analog
      });

    /// <summary>
    /// Input select by 1-based index and feedback (0 = none / unmatched)
    /// </summary>
    [JoinName("InputSelectIndex")]
    public JoinDataComplete InputSelectIndex = new JoinDataComplete(
      new JoinData { JoinNumber = 2, JoinSpan = 1 },
      new JoinMetadata
      {
        Description = "Input Select by 1-based index & feedback (0 = none / unmatched)",
        JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
        JoinType = eJoinType.Analog
      });

    /// <summary>
    /// Communication monitor status (0 = Unknown, 1 = Ok, 2 = Warning, 3 = Error)
    /// </summary>
    [JoinName("CommunicationStatus")]
    public JoinDataComplete CommunicationStatus = new JoinDataComplete(
      new JoinData { JoinNumber = 3, JoinSpan = 1 },
      new JoinMetadata
      {
        Description = "Communication Monitor status (MonitorStatus as ushort)",
        JoinCapabilities = eJoinCapabilities.ToSIMPL,
        JoinType = eJoinType.Analog
      });

    #endregion

    #region Serial

    /// <summary>
    /// Device name from config
    /// </summary>
    [JoinName("DeviceName")]
    public JoinDataComplete DeviceName = new JoinDataComplete(
      new JoinData { JoinNumber = 1, JoinSpan = 1 },
      new JoinMetadata
      {
        Description = "Device Name",
        JoinCapabilities = eJoinCapabilities.ToSIMPL,
        JoinType = eJoinType.Serial
      });

    /// <summary>
    /// Name of the currently selected input
    /// </summary>
    [JoinName("CurrentInputName")]
    public JoinDataComplete CurrentInputName = new JoinDataComplete(
      new JoinData { JoinNumber = 2, JoinSpan = 1 },
      new JoinMetadata
      {
        Description = "Current Input Name",
        JoinCapabilities = eJoinCapabilities.ToSIMPL,
        JoinType = eJoinType.Serial
      });

    /// <summary>
    /// Device model (from /device/id)
    /// </summary>
    [JoinName("Model")]
    public JoinDataComplete Model = new JoinDataComplete(
      new JoinData { JoinNumber = 3, JoinSpan = 1 },
      new JoinMetadata
      {
        Description = "Model",
        JoinCapabilities = eJoinCapabilities.ToSIMPL,
        JoinType = eJoinType.Serial
      });

    /// <summary>
    /// Serial number / barcode (from /device/id)
    /// </summary>
    [JoinName("SerialNumber")]
    public JoinDataComplete SerialNumber = new JoinDataComplete(
      new JoinData { JoinNumber = 4, JoinSpan = 1 },
      new JoinMetadata
      {
        Description = "Serial Number (barcode)",
        JoinCapabilities = eJoinCapabilities.ToSIMPL,
        JoinType = eJoinType.Serial
      });

    /// <summary>
    /// Firmware version (from /device/info)
    /// </summary>
    [JoinName("FirmwareVersion")]
    public JoinDataComplete FirmwareVersion = new JoinDataComplete(
      new JoinData { JoinNumber = 5, JoinSpan = 1 },
      new JoinMetadata
      {
        Description = "Firmware Version",
        JoinCapabilities = eJoinCapabilities.ToSIMPL,
        JoinType = eJoinType.Serial
      });

    /// <summary>
    /// MAC address (from /device/id)
    /// </summary>
    [JoinName("MacAddress")]
    public JoinDataComplete MacAddress = new JoinDataComplete(
      new JoinData { JoinNumber = 6, JoinSpan = 1 },
      new JoinMetadata
      {
        Description = "MAC Address",
        JoinCapabilities = eJoinCapabilities.ToSIMPL,
        JoinType = eJoinType.Serial
      });

    /// <summary>
    /// IP address / host from config
    /// </summary>
    [JoinName("IpAddress")]
    public JoinDataComplete IpAddress = new JoinDataComplete(
      new JoinData { JoinNumber = 7, JoinSpan = 1 },
      new JoinMetadata
      {
        Description = "IP Address / host",
        JoinCapabilities = eJoinCapabilities.ToSIMPL,
        JoinType = eJoinType.Serial
      });

    /// <summary>
    /// Input names in config order
    /// </summary>
    [JoinName("InputNames")]
    public JoinDataComplete InputNames = new JoinDataComplete(
      new JoinData { JoinNumber = 11, JoinSpan = MaxInputs },
      new JoinMetadata
      {
        Description = "Input Names (config order)",
        JoinCapabilities = eJoinCapabilities.ToSIMPL,
        JoinType = eJoinType.Serial
      });

    #endregion

    /// <summary>
    /// Creates the join map starting at joinStart
    /// </summary>
    public GenelecSmartIpBridgeJoinMap(uint joinStart)
      : base(joinStart, typeof(GenelecSmartIpBridgeJoinMap))
    {
    }
  }
}
