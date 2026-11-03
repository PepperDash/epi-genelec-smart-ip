using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.Plugins
{
  /// <summary>
  /// Comm monitor driven by REST poll results rather than a socket. One failed poll = warning,
  /// three consecutive failures = error.
  /// </summary>
  public class GenelecSmartIpCommunicationMonitor : StatusMonitorBase
  {
    private int failures;

    /// <summary>
    /// Creates the monitor
    /// </summary>
    public GenelecSmartIpCommunicationMonitor(IKeyed parent) : base(parent, 5000, 10000)
    {
    }

    /// <summary>
    /// Call after a successful poll
    /// </summary>
    public void ReportSuccess()
    {
      failures = 0;
      Status = MonitorStatus.IsOk;
    }

    /// <summary>
    /// Call after a failed poll
    /// </summary>
    public void ReportFailure()
    {
      failures++;
      Status = failures >= 3 ? MonitorStatus.InError : MonitorStatus.InWarning;
    }

    /// <inheritdoc />
    public override void Start() { }

    /// <inheritdoc />
    public override void Stop() { }
  }
}
