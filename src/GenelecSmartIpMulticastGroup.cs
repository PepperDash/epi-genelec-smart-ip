using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using PepperDash.Core;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.DeviceInfo;

namespace PepperDash.Essentials.Plugins
{
  /// <summary>
  /// Controls Genelec Smart IP speakers as one group through the Smart IP multicast API: a single message reaches
  /// every speaker listening on the group's address. Two groups: the volume group gets level, mute and power;
  /// the optional mute group (e.g. a fixed-level subwoofer on its own address) gets mute and power only.
  /// <para>
  /// The multicast API has no replies, so feedback comes from the listed members, ordinary genelecSmartIp
  /// devices that keep polling each speaker. Level and mute are learned from the first volume member's own reports
  /// (nothing is sent from a placeholder; relative commands wait until the value is known). After each command
  /// settles, members' polled values are compared with the group and a value a member missed (lost datagram) is
  /// re-sent to that member's group.
  /// </para>
  /// </summary>
  public class GenelecSmartIpMulticastGroup : EssentialsDevice, IBasicVolumeWithFeedback, IHasPowerControlWithFeedback
  {
    private const int MaxRampSteps = 200;
    private const int RepairTickMs = 2000;

    private readonly GenelecSmartIpMulticastGroupConfig config;
    private readonly IPEndPoint volumeTarget;
    private readonly IPEndPoint muteTarget;   // null when there is no mute group
    private readonly object sync = new object();
    private readonly List<Action> unsubscribe = new List<Action>();
    private readonly HashSet<string> warnedMembers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly double minDb;
    private readonly double maxDb;

    private GenelecSmartIpMulticastSender sender;
    private List<GenelecSmartIpDevice> volumeMembers = new List<GenelecSmartIpDevice>();
    private List<GenelecSmartIpDevice> muteMembers = new List<GenelecSmartIpDevice>();
    private GenelecSmartIpDevice source;   // first volume member: level/mute are learned from it

    private bool active;
    private double levelDb;
    private bool muted;
    private bool levelKnown;
    private bool muteKnown;
    private DateTime lastCommandUtc = DateTime.MinValue;

    private Timer rampTimer;
    private int rampGeneration;
    private int rampSteps;
    private Timer repairTimer;

    /// <summary>
    /// Creates the group. The config must already be validated (see the factory)
    /// </summary>
    public GenelecSmartIpMulticastGroup(string key, string name, GenelecSmartIpMulticastGroupConfig config)
        : base(key, name)
    {
      this.config = config;
      minDb = Math.Max(GenelecSmartIpMulticast.MinDb, Math.Min(config.VolumeMinDb, config.VolumeMaxDb));
      maxDb = Math.Min(GenelecSmartIpMulticast.MaxDb, Math.Max(config.VolumeMinDb, config.VolumeMaxDb));

      volumeTarget = new IPEndPoint(IPAddress.Parse(config.VolumeGroup.Address.Trim()), config.VolumeGroup.Port);
      if (config.MuteGroup != null)
        muteTarget = new IPEndPoint(IPAddress.Parse(config.MuteGroup.Address.Trim()), config.MuteGroup.Port);

      // until a value is known, show the source's current value rather than a group default
      VolumeLevelFeedback = new IntFeedback(key + "-volume", () =>
          GenelecSmartIpVolumeScaling.ToScaled(levelKnown || source == null ? levelDb : source.LevelDb, minDb, maxDb));
      MuteFeedback = new BoolFeedback(key + "-mute", () =>
          muteKnown || source == null ? muted : source.MuteFeedback.BoolValue);
      PowerIsOnFeedback = new BoolFeedback(key + "-power", () => source != null && source.PowerIsOnFeedback.BoolValue);
    }

    /// <inheritdoc />
    public IntFeedback VolumeLevelFeedback { get; private set; }

    /// <inheritdoc />
    public BoolFeedback MuteFeedback { get; private set; }

    /// <summary>
    /// Power state of the first volume member (the multicast API itself has no feedback)
    /// </summary>
    public BoolFeedback PowerIsOnFeedback { get; private set; }

    /// <inheritdoc />
    public override bool CustomActivate()
    {
      var vol = Resolve(config.VolumeGroup.Members, "volumeGroup.members");
      var volKeys = new HashSet<string>(vol.Select(m => m.Key), StringComparer.OrdinalIgnoreCase);
      var mute = config.MuteGroup != null
          ? Resolve(config.MuteGroup.Members, "muteGroup.members").Where(m => !volKeys.Contains(m.Key)).ToList()
          : new List<GenelecSmartIpDevice>();

      foreach (var m in vol.Where(m => m.HasFixedLevel))
        this.LogWarning("volumeGroup member {Key} has fixedLevelDb: it puts its level back after every group volume change; " +
                        "move it to muteGroup (its own multicast address) or remove fixedLevelDb", m.Key);
      if (muteTarget != null && muteTarget.Equals(volumeTarget))
        this.LogWarning("muteGroup uses the same address and port as volumeGroup, so its speakers also receive level messages");

      lock (sync)
      {
        DetachLocked();
        try
        {
          sender = new GenelecSmartIpMulticastSender(config.LocalAddress);
        }
        catch (Exception ex)
        {
          this.LogError(ex, "Multicast sender could not start ({Message}); the group sends nothing", ex.Message);
          sender = null;
        }

        volumeMembers = vol;
        muteMembers = mute;
        source = vol.FirstOrDefault();
        levelKnown = muteKnown = false;
        warnedMembers.Clear();
        active = true;

        foreach (var m in AllMembers()) Subscribe(m);
        repairTimer = new Timer(_ => RepairTick(), null, RepairTickMs, RepairTickMs);

        VolumeLevelFeedback.FireUpdate();
        MuteFeedback.FireUpdate();
        PowerIsOnFeedback.FireUpdate();
      }

      this.LogInformation("Multicast group: volume {Volume} ({VolumeMembers}); mute {Mute} ({MuteMembers}); range {Min}..{Max} dB",
          volumeTarget, Describe(vol), muteTarget != null ? muteTarget.ToString() : "none", Describe(mute), minDb, maxDb);
      if (source == null)
        this.LogWarning("volumeGroup has no genelecSmartIp members: the group has no feedback, and volume up/down and mute toggle stay disabled");

      return base.CustomActivate();
    }

    /// <summary>
    /// Detaches every handler, stops the ramp and repair timers and closes the socket
    /// </summary>
    public override bool Deactivate()
    {
      lock (sync)
      {
        active = false;
        DetachLocked();
      }
      return base.Deactivate();
    }

    private void DetachLocked()
    {
      foreach (var action in unsubscribe) action();
      unsubscribe.Clear();
      StopRampLocked();
      if (repairTimer != null) { repairTimer.Dispose(); repairTimer = null; }
      if (sender != null) { sender.Dispose(); sender = null; }
    }

    #region Commands

    /// <inheritdoc />
    public void SetVolume(ushort level)
    {
      lock (sync)
      {
        if (!active) return;
        StopRampLocked();
        ApplyLevelLocked(GenelecSmartIpVolumeScaling.ToDb(level, minDb, maxDb));
      }
    }

    /// <inheritdoc />
    public void VolumeUp(bool pressRelease) { Ramp(pressRelease, +1); }

    /// <inheritdoc />
    public void VolumeDown(bool pressRelease) { Ramp(pressRelease, -1); }

    /// <inheritdoc />
    public void MuteOn() { SetMute(true); }

    /// <inheritdoc />
    public void MuteOff() { SetMute(false); }

    /// <inheritdoc />
    public void MuteToggle()
    {
      lock (sync)
      {
        if (!active) return;
        if (!muteKnown)
        {
          this.LogDebug("MuteToggle ignored: mute not reported yet (MuteOn/MuteOff still work)");
          return;
        }
        SetMuteLocked(!muted);
      }
    }

    /// <summary>Wakes every speaker in both groups (multicast "BOOT")</summary>
    public void PowerOn() { SendPower(GenelecSmartIpMulticast.StateBoot); }

    /// <summary>Puts every speaker in both groups in standby (multicast "STANDBY")</summary>
    public void PowerOff() { SendPower(GenelecSmartIpMulticast.StateStandby); }

    /// <summary>Standby when the first volume member is on, otherwise wake</summary>
    public void PowerToggle()
    {
      if (PowerIsOnFeedback.BoolValue) PowerOff(); else PowerOn();
    }

    private void SetMute(bool mute)
    {
      lock (sync)
      {
        if (!active) return;
        SetMuteLocked(mute);
      }
    }

    private void SendPower(string state)
    {
      lock (sync)
      {
        if (!active) return;
        var payload = GenelecSmartIpMulticast.PowerMessage(state);
        SendLocked(volumeTarget, payload, "power " + state);
        if (muteTarget != null) SendLocked(muteTarget, payload, "power " + state);
        lastCommandUtc = DateTime.UtcNow;
      }
    }

    private void Ramp(bool press, int direction)
    {
      lock (sync)
      {
        if (!active) return;
        StopRampLocked();
        if (!press) return;
        if (!levelKnown)
        {
          this.LogDebug("Volume {Direction} ignored: level not reported yet (SetVolume still works)", direction > 0 ? "up" : "down");
          return;
        }

        rampSteps = 0;
        var generation = ++rampGeneration;
        var step = Math.Max(0.1, Math.Abs(config.VolumeStepDb)) * direction;
        rampTimer = new Timer(_ =>
        {
          lock (sync)
          {
            if (!active || generation != rampGeneration || rampTimer == null) return;
            if (++rampSteps > MaxRampSteps) { StopRampLocked(); return; }
            var next = Math.Max(minDb, Math.Min(maxDb, levelDb + step));
            if (!GenelecSmartIpMulticast.LevelDiffers(next, levelDb)) return;
            ApplyLevelLocked(next);
          }
        }, null, 0, Math.Max(100, config.VolumeRampIntervalMs));
      }
    }

    private void StopRampLocked()
    {
      rampGeneration++;
      if (rampTimer != null) { rampTimer.Dispose(); rampTimer = null; }
    }

    private void ApplyLevelLocked(double db)
    {
      levelDb = GenelecSmartIpMulticast.ClampDb(Math.Max(minDb, Math.Min(maxDb, db)));
      levelKnown = true;
      lastCommandUtc = DateTime.UtcNow;
      VolumeLevelFeedback.FireUpdate();
      SendLocked(volumeTarget, GenelecSmartIpMulticast.LevelMessage(levelDb), "level " + levelDb.ToString("0.0"));
    }

    private void SetMuteLocked(bool mute)
    {
      muted = mute;
      muteKnown = true;
      lastCommandUtc = DateTime.UtcNow;
      MuteFeedback.FireUpdate();
      var payload = GenelecSmartIpMulticast.MuteMessage(mute);
      SendLocked(volumeTarget, payload, "mute " + mute);
      if (muteTarget != null) SendLocked(muteTarget, payload, "mute " + mute);
    }

    private void SendLocked(IPEndPoint target, byte[] payload, string what)
    {
      if (sender == null) return;
      var error = sender.Send(target, payload, Math.Max(1, config.SendCount));
      if (error != null)
        this.LogWarning("Multicast {What} to {Target} failed: {Error}", what, target, error);
      else
        this.LogVerbose("Multicast {What} -> {Target}", what, target);
    }

    #endregion

    #region Learning, feedback and repair

    private void Subscribe(GenelecSmartIpDevice member)
    {
      EventHandler<FeedbackEventArgs> level = (s, e) => MemberLevelChanged(member);
      EventHandler<FeedbackEventArgs> mute = (s, e) => MemberMuteChanged(member);
      EventHandler<FeedbackEventArgs> power = (s, e) => { if (member == source) PowerIsOnFeedback.FireUpdate(); };
      DeviceInfoChangeHandler info = (s, e) => CheckMembership(member);

      member.VolumeLevelFeedback.OutputChange += level;
      member.MuteFeedback.OutputChange += mute;
      member.PowerIsOnFeedback.OutputChange += power;
      member.DeviceInfoChanged += info;
      unsubscribe.Add(() =>
      {
        member.VolumeLevelFeedback.OutputChange -= level;
        member.MuteFeedback.OutputChange -= mute;
        member.PowerIsOnFeedback.OutputChange -= power;
        member.DeviceInfoChanged -= info;
      });

      if (member.MulticastAddress != null) CheckMembership(member);   // device info already read
    }

    /// <summary>A member's level changed: learn it from the source if the group doesn't know its level yet</summary>
    private void MemberLevelChanged(GenelecSmartIpDevice member)
    {
      lock (sync)
      {
        if (!active || member != source) return;
        if (!levelKnown)
        {
          levelDb = GenelecSmartIpMulticast.ClampDb(member.LevelDb);
          levelKnown = true;
          this.LogInformation("Learned level {Level} dB from {Source}", levelDb, member.Key);
        }
        VolumeLevelFeedback.FireUpdate();
      }
    }

    private void MemberMuteChanged(GenelecSmartIpDevice member)
    {
      lock (sync)
      {
        if (!active || member != source) return;
        if (!muteKnown)
        {
          muted = member.MuteFeedback.BoolValue;
          muteKnown = true;
          this.LogInformation("Learned mute {Mute} from {Source}", muted, member.Key);
        }
        MuteFeedback.FireUpdate();
      }
    }

    /// <summary>
    /// Every few seconds, once the last command has settled (members polled since): re-send the group's level
    /// to the volume group if a volume member differs, and its mute to any group with a member that differs.
    /// Covers a datagram a speaker missed. Members that are offline or in standby are skipped.
    /// </summary>
    private void RepairTick()
    {
      lock (sync)
      {
        if (!active || sender == null || rampTimer != null) return;
        if ((DateTime.UtcNow - lastCommandUtc).TotalMilliseconds < Math.Max(1000, config.RepairDelayMs)) return;

        var levelOff = levelKnown
            ? volumeMembers.Where(m => Listening(m) && GenelecSmartIpMulticast.LevelDiffers(m.LevelDb, levelDb)).ToList()
            : new List<GenelecSmartIpDevice>();
        var muteOffVolume = muteKnown
            ? volumeMembers.Where(m => Listening(m) && m.MuteFeedback.BoolValue != muted).ToList()
            : new List<GenelecSmartIpDevice>();
        var muteOffMute = muteKnown
            ? muteMembers.Where(m => Listening(m) && m.MuteFeedback.BoolValue != muted).ToList()
            : new List<GenelecSmartIpDevice>();

        if (levelOff.Count == 0 && muteOffVolume.Count == 0 && muteOffMute.Count == 0) return;

        if (levelOff.Count > 0)
        {
          this.LogInformation("Re-sending level {Level} dB to the volume group: {Members} differ", levelDb, Describe(levelOff));
          SendLocked(volumeTarget, GenelecSmartIpMulticast.LevelMessage(levelDb), "level (repair)");
        }
        if (muteOffVolume.Count > 0)
        {
          this.LogInformation("Re-sending mute {Mute} to the volume group: {Members} differ", muted, Describe(muteOffVolume));
          SendLocked(volumeTarget, GenelecSmartIpMulticast.MuteMessage(muted), "mute (repair)");
        }
        if (muteOffMute.Count > 0 && muteTarget != null)
        {
          this.LogInformation("Re-sending mute {Mute} to the mute group: {Members} differ", muted, Describe(muteOffMute));
          SendLocked(muteTarget, GenelecSmartIpMulticast.MuteMessage(muted), "mute (repair)");
        }
        // give the members a poll to pick it up before comparing again
        lastCommandUtc = DateTime.UtcNow;
      }
    }

    /// <summary>A member's level/mute are only meaningful while it's online and on (standby speakers aren't polled for volume)</summary>
    private static bool Listening(GenelecSmartIpDevice member)
    {
      return member.CommunicationMonitor.IsOnlineFeedback.BoolValue && member.PowerIsOnFeedback.BoolValue;
    }

    /// <summary>Warns once if a member's speaker is not configured for the group it is listed in</summary>
    private void CheckMembership(GenelecSmartIpDevice member)
    {
      IPEndPoint target;
      string groupName;
      lock (sync)
      {
        if (!active) return;
        if (volumeMembers.Contains(member)) { target = volumeTarget; groupName = "volumeGroup"; }
        else if (muteMembers.Contains(member) && muteTarget != null) { target = muteTarget; groupName = "muteGroup"; }
        else return;
      }

      var problem = GenelecSmartIpMulticast.CompareSpeakerGroup(member.MulticastAddress, member.MulticastPort,
          target.Address.ToString(), target.Port);
      lock (sync)
      {
        if (problem == null) { warnedMembers.Remove(member.Key); return; }
        if (!warnedMembers.Add(member.Key)) return;
      }
      this.LogWarning("{Group} member {Key}: {Problem}. It won't hear the group's messages; set its multicast address/port in Smart IP Manager",
          groupName, member.Key, problem);
    }

    #endregion

    #region Helpers

    private IEnumerable<GenelecSmartIpDevice> AllMembers() { return volumeMembers.Concat(muteMembers); }

    private List<GenelecSmartIpDevice> Resolve(IEnumerable<string> keys, string listName)
    {
      var result = new List<GenelecSmartIpDevice>();
      foreach (var key in (keys ?? Enumerable.Empty<string>()).Where(k => !string.IsNullOrWhiteSpace(k))
                   .Select(k => k.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
      {
        var device = DeviceManager.GetDeviceForKey(key);
        var member = device as GenelecSmartIpDevice;
        if (member != null)
          result.Add(member);
        else
          this.LogWarning("{List}: '{Key}' {Problem}; it is ignored", listName, key,
              device == null ? "was not found" : "is not a genelecSmartIp device");
      }
      return result;
    }

    private static string Describe(IEnumerable<IKeyed> devices)
    {
      var keys = devices.Select(d => d.Key).ToList();
      return keys.Count > 0 ? string.Join(", ", keys) : "none";
    }

    #endregion
  }
}
