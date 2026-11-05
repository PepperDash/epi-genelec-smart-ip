using System;

namespace PepperDash.Essentials.Plugins
{
  /// <summary>
  /// What a successful GET /audio/volume poll put into a speaker device's level and mute
  /// </summary>
  public class GenelecSmartIpVolumeReportEventArgs : EventArgs
  {
    /// <summary>
    /// Creates the event args
    /// </summary>
    public GenelecSmartIpVolumeReportEventArgs(bool levelReported, bool muteReported)
    {
      LevelReported = levelReported;
      MuteReported = muteReported;
    }

    /// <summary>
    /// The speaker's level was read and applied. False when the reply had no level, or a level write was
    /// pending or overlapped the poll (the device kept the newer value it is sending)
    /// </summary>
    public bool LevelReported { get; private set; }

    /// <summary>
    /// The speaker's mute was read and applied
    /// </summary>
    public bool MuteReported { get; private set; }
  }
}
