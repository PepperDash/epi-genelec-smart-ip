// linked into the nullable-enabled test project; the plugin itself does not use nullable annotations
#nullable disable

using System;
using System.Threading.Tasks;

namespace PepperDash.Essentials.Plugins
{
  /// <summary>
  /// Sends only the most recent value when updates arrive faster than the device accepts them.
  /// At most one send is in flight; values queued while it runs collapse to the newest one, which
  /// is sent next. A volume ramp or a mirrored codec fader therefore produces a handful of requests
  /// instead of one per step (the speaker allows only four connections and asks for light traffic).
  /// Pure logic (no Crestron dependencies) so it can be unit tested.
  /// </summary>
  public class GenelecSmartIpLatestValueSender<T>
  {
    private readonly Func<T, Task> send;
    private readonly Action<Exception> onError;
    private readonly object sync = new object();

    private bool hasPending;
    private T pending;
    private bool sending;

    /// <summary>
    /// Creates the sender
    /// </summary>
    /// <param name="send">performs one send; awaited before the next value goes out</param>
    /// <param name="onError">called if a send throws; the next queued value is still sent</param>
    public GenelecSmartIpLatestValueSender(Func<T, Task> send, Action<Exception> onError = null)
    {
      if (send == null) throw new ArgumentNullException("send");
      this.send = send;
      this.onError = onError;
    }

    /// <summary>
    /// True while a send is running or a value is waiting to be sent
    /// </summary>
    public bool IsBusy
    {
      get { lock (sync) return sending || hasPending; }
    }

    /// <summary>
    /// Queues a value. Replaces any value not yet sent; starts sending if idle
    /// </summary>
    public void Post(T value)
    {
      lock (sync)
      {
        pending = value;
        hasPending = true;
        if (sending) return;
        sending = true;
      }

      Task.Run(RunAsync);
    }

    private async Task RunAsync()
    {
      while (true)
      {
        T value;
        lock (sync)
        {
          if (!hasPending)
          {
            sending = false;
            return;
          }
          value = pending;
          hasPending = false;
          pending = default(T);
        }

        try
        {
          await send(value).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
          if (onError != null) onError(ex);
        }
      }
    }
  }
}
