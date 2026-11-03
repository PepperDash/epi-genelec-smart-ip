using System;

namespace PepperDash.Essentials.Plugins
{
  /// <summary>
  /// Pure-logic dB &lt;-&gt; 0-65535 scaling helpers (no Crestron dependencies so they can be unit tested)
  /// </summary>
  public static class GenelecSmartIpVolumeScaling
  {
    /// <summary>
    /// Converts a dB level to the linear 0-65535 scale used by IBasicVolumeWithFeedback
    /// </summary>
    public static ushort ToScaled(double levelDb, double minDb, double maxDb)
    {
      if (maxDb <= minDb) return 0;
      var pct = (levelDb - minDb) / (maxDb - minDb);
      pct = Math.Max(0, Math.Min(1, pct));
      return (ushort)Math.Round(pct * 65535.0);
    }

    /// <summary>
    /// Converts a 0-65535 scaled level to dB, rounded to the device's 0.1 dB resolution
    /// </summary>
    public static double ToDb(ushort scaled, double minDb, double maxDb)
    {
      var db = minDb + (scaled / 65535.0) * (maxDb - minDb);
      return Math.Round(db, 1);
    }

    /// <summary>
    /// Clamps a dB value to the configured range, rounded to 0.1 dB
    /// </summary>
    public static double Clamp(double db, double minDb, double maxDb)
    {
      return Math.Round(Math.Max(minDb, Math.Min(maxDb, db)), 1);
    }
  }
}
