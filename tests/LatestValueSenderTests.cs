using System.Collections.Concurrent;
using FluentAssertions;
using Xunit;

namespace PepperDash.Essentials.Plugins.Tests;

public class LatestValueSenderTests
{
  private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 3000)
  {
    var end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
    while (!condition())
    {
      if (DateTime.UtcNow > end) throw new TimeoutException("condition not met");
      await Task.Delay(10);
    }
  }

  [Fact]
  public async Task Single_Post_Is_Sent_Once()
  {
    var sent = new ConcurrentQueue<double>();
    var sender = new GenelecSmartIpLatestValueSender<double>(v => { sent.Enqueue(v); return Task.CompletedTask; });

    sender.Post(-20.0);

    await WaitUntil(() => !sender.IsBusy);
    sent.Should().Equal(-20.0);
  }

  [Fact]
  public async Task Burst_While_Sending_Collapses_To_Latest()
  {
    var sent = new ConcurrentQueue<double>();
    var gate = new TaskCompletionSource<bool>();
    var first = true;
    var sender = new GenelecSmartIpLatestValueSender<double>(async v =>
    {
      sent.Enqueue(v);
      if (first) { first = false; await gate.Task; }
    });

    sender.Post(-60.0);                      // starts sending, held at the gate
    await WaitUntil(() => sent.Count == 1);
    for (var i = 0; i < 100; i++)            // a fader ramp while the first request is in flight
      sender.Post(-59.0 + i * 0.5);
    gate.SetResult(true);

    await WaitUntil(() => !sender.IsBusy);
    sent.Should().Equal(-60.0, -59.0 + 99 * 0.5);   // first value, then only the newest
  }

  [Fact]
  public async Task Only_One_Send_In_Flight()
  {
    var inFlight = 0;
    var maxInFlight = 0;
    var sender = new GenelecSmartIpLatestValueSender<int>(async v =>
    {
      var now = Interlocked.Increment(ref inFlight);
      InterlockedMax(ref maxInFlight, now);
      await Task.Delay(5);
      Interlocked.Decrement(ref inFlight);
    });

    var posters = Enumerable.Range(0, 8).Select(t => Task.Run(() =>
    {
      for (var i = 0; i < 200; i++) sender.Post(t * 1000 + i);
    }));
    await Task.WhenAll(posters);

    await WaitUntil(() => !sender.IsBusy);
    maxInFlight.Should().Be(1);
  }

  [Fact]
  public async Task Failed_Send_Is_Reported_And_Next_Value_Still_Goes_Out()
  {
    var sent = new ConcurrentQueue<double>();
    var errors = new ConcurrentQueue<Exception>();
    var gate = new TaskCompletionSource<bool>();
    var calls = 0;
    var sender = new GenelecSmartIpLatestValueSender<double>(async v =>
    {
      if (Interlocked.Increment(ref calls) == 1)
      {
        await gate.Task;
        throw new InvalidOperationException("speaker offline");
      }
      sent.Enqueue(v);
    }, errors.Enqueue);

    sender.Post(-10.0);
    await WaitUntil(() => calls == 1);
    sender.Post(-11.0);
    gate.SetResult(true);

    await WaitUntil(() => !sender.IsBusy);
    errors.Should().ContainSingle().Which.Message.Should().Be("speaker offline");
    sent.Should().Equal(-11.0);
  }

  [Fact]
  public async Task Idle_Sender_Restarts_On_Next_Post()
  {
    var sent = new ConcurrentQueue<double>();
    var sender = new GenelecSmartIpLatestValueSender<double>(v => { sent.Enqueue(v); return Task.CompletedTask; });

    sender.Post(-1.0);
    await WaitUntil(() => !sender.IsBusy);
    sender.Post(-2.0);
    await WaitUntil(() => !sender.IsBusy);

    sent.Should().Equal(-1.0, -2.0);
  }

  private static void InterlockedMax(ref int target, int value)
  {
    int current;
    while ((current = Volatile.Read(ref target)) < value
           && Interlocked.CompareExchange(ref target, value, current) != current) { }
  }
}
