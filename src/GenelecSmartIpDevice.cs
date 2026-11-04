using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.DeviceInfo;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using DeviceInfoModel = PepperDash.Essentials.Core.DeviceInfo.DeviceInfo;

namespace PepperDash.Essentials.Plugins
{
  /// <summary>
  /// Genelec Smart IP loudspeaker controlled over the unicast REST API.
  /// Mobile control messengers (volume, power, inputs, comm monitor, device info) are created automatically
  /// by Essentials for the core interfaces implemented here; SIMPL control is available through the EISC bridge.
  /// </summary>
  public class GenelecSmartIpDevice : EssentialsBridgeableDevice,
      IBasicVolumeWithFeedback,
      IHasPowerControlWithFeedback,
      IHasInputs<string>,
      ICommunicationMonitor,
      IDeviceInfoProvider
  {
    private const string StateActive = "ACTIVE";
    private const string StateStandby = "STANDBY";
    private const int MaxRampSteps = 200;

    private readonly GenelecSmartIpPropertiesConfig config;
    private readonly GenelecSmartIpRestClient rest;
    private readonly GenelecSmartIpInputs inputs = new GenelecSmartIpInputs();
    private readonly List<GenelecSmartIpInputItem> inputOrder = new List<GenelecSmartIpInputItem>();
    private readonly GenelecSmartIpCommunicationMonitor monitor;
    private readonly object stateLock = new object();
    private readonly double? fixedLevelDb;

    private Timer pollTimer;
    private Timer rampTimer;
    private int polling;
    private int rampSteps;
    private int deviceInfoStale = 1;

    private double levelDb;
    private bool muted;
    private string powerState = string.Empty;
    private string model = string.Empty;

    /// <summary>
    /// Creates the device
    /// </summary>
    public GenelecSmartIpDevice(string key, string name, GenelecSmartIpPropertiesConfig config)
        : base(key, name)
    {
      this.config = config;

      rest = new GenelecSmartIpRestClient(this, config.Host, config.Port, config.Username, config.Password, config.RequestTimeoutMs);
      monitor = new GenelecSmartIpCommunicationMonitor(this);

      levelDb = config.VolumeMinDb;

      bool rejected;
      fixedLevelDb = GenelecSmartIpVolumeScaling.NormalizeFixedLevel(config.FixedLevelDb, out rejected);
      if (rejected)
      {
        this.LogWarning("fixedLevelDb {Level} is outside {Min}..{Max} dB; ignoring it, volume stays under normal control",
            config.FixedLevelDb, GenelecSmartIpVolumeScaling.ApiMinDb, GenelecSmartIpVolumeScaling.ApiMaxDb);
      }
      else if (fixedLevelDb.HasValue)
      {
        this.LogInformation("Level fixed at {Level} dB (fixedLevelDb); volume commands will be ignored", fixedLevelDb.Value);
      }

      DeviceInfo = new DeviceInfoModel { IpAddress = config.Host };

      PowerIsOnFeedback = new BoolFeedback("PowerIsOn", () => powerState == StateActive);
      PowerIsOffFeedback = new BoolFeedback("PowerIsOff", () => powerState != StateActive);
      MuteFeedback = new BoolFeedback("Mute", () => muted);
      MuteIsOffFeedback = new BoolFeedback("MuteIsOff", () => !muted);
      VolumeLevelFeedback = new IntFeedback("VolumeLevel", () =>
          GenelecSmartIpVolumeScaling.ToScaled(levelDb, config.VolumeMinDb, config.VolumeMaxDb));
      CurrentInputIndexFeedback = new IntFeedback("CurrentInputIndex", () =>
          inputOrder.FindIndex(i => i.Key == inputs.CurrentItem) + 1);
      CurrentInputNameFeedback = new StringFeedback("CurrentInputName", () =>
      {
        var item = inputOrder.FirstOrDefault(i => i.Key == inputs.CurrentItem);
        return item != null ? item.Name : string.Empty;
      });
      ModelFeedback = new StringFeedback("Model", () => model);

      BuildInputs();

      inputs.CurrentItemChanged += (s, e) =>
      {
        CurrentInputIndexFeedback.FireUpdate();
        CurrentInputNameFeedback.FireUpdate();
      };
    }

    #region Properties

    /// <inheritdoc />
    public BoolFeedback PowerIsOnFeedback { get; private set; }

    /// <summary>
    /// True when the speaker is not ACTIVE (STANDBY, ISS_SLEEP, PWR_FAIL or unknown)
    /// </summary>
    public BoolFeedback PowerIsOffFeedback { get; private set; }

    /// <inheritdoc />
    public BoolFeedback MuteFeedback { get; private set; }

    /// <summary>
    /// Inverse of MuteFeedback
    /// </summary>
    public BoolFeedback MuteIsOffFeedback { get; private set; }

    /// <inheritdoc />
    public IntFeedback VolumeLevelFeedback { get; private set; }

    /// <summary>
    /// 1-based position (config order) of the current input, 0 when the device's input set matches no item
    /// </summary>
    public IntFeedback CurrentInputIndexFeedback { get; private set; }

    /// <summary>
    /// Name of the current input, empty when unmatched
    /// </summary>
    public StringFeedback CurrentInputNameFeedback { get; private set; }

    /// <summary>
    /// Model name reported by /device/id
    /// </summary>
    public StringFeedback ModelFeedback { get; private set; }

    /// <inheritdoc />
    public ISelectableItems<string> Inputs { get { return inputs; } }

    /// <inheritdoc />
    public StatusMonitorBase CommunicationMonitor { get { return monitor; } }

    /// <inheritdoc />
    public DeviceInfoModel DeviceInfo { get; private set; }

    /// <inheritdoc />
    public event DeviceInfoChangeHandler DeviceInfoChanged;

    #endregion

    #region Lifecycle

    /// <inheritdoc />
    public override void Initialize()
    {
      base.Initialize();

      this.LogInformation("Starting poll of {Host}:{Port} every {PollMs} ms", config.Host, config.Port, config.PollTimeMs);
      pollTimer = new Timer(_ => Poll(), null, 0, Math.Max(1000, config.PollTimeMs));
    }

    /// <summary>
    /// Polls the device. Only /device/pwr is read in standby since other endpoints may not respond.
    /// </summary>
    public void Poll()
    {
      // skip if a previous poll is still in flight
      if (Interlocked.Exchange(ref polling, 1) == 1) return;

      Task.Run(async () =>
      {
        try
        {
          await RefreshAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
          this.LogError(ex, "Poll failed: {Message}", ex.Message);
          ReportPollFailure();
        }
        finally
        {
          Interlocked.Exchange(ref polling, 0);
        }
      });
    }

    private async Task RefreshAsync()
    {
      var pwr = await rest.GetAsync("/device/pwr").ConfigureAwait(false);
      if (pwr == null)
      {
        ReportPollFailure();
        return;
      }

      monitor.ReportSuccess();
      SetPowerState((string)pwr["state"]);

      if (powerState != StateActive) return;

      var vol = await rest.GetAsync("/audio/volume").ConfigureAwait(false);
      if (vol != null && Volatile.Read(ref rampSteps) == 0)
      {
        ApplyVolume(vol);
      }

      if (vol != null && fixedLevelDb.HasValue)
      {
        await EnforceFixedLevelAsync(vol, fixedLevelDb.Value).ConfigureAwait(false);
      }

      var inp = await rest.GetAsync("/audio/inputs").ConfigureAwait(false);
      if (inp != null) ApplyInputs(inp);

      if (Volatile.Read(ref deviceInfoStale) == 1)
      {
        await RefreshDeviceInfoAsync().ConfigureAwait(false);
      }
    }

    private void ReportPollFailure()
    {
      monitor.ReportFailure();
      // the speaker may have rebooted or been swapped; re-read identity once it is back
      Interlocked.Exchange(ref deviceInfoStale, 1);
    }

    #endregion

    #region Device info

    /// <summary>
    /// Marks device info stale and polls. Identity endpoints are only read while the speaker is ACTIVE.
    /// </summary>
    public void UpdateDeviceInfo()
    {
      Interlocked.Exchange(ref deviceInfoStale, 1);
      Poll();
    }

    private async Task RefreshDeviceInfoAsync()
    {
      var id = await rest.GetAsync("/device/id").ConfigureAwait(false);
      var info = await rest.GetAsync("/device/info").ConfigureAwait(false);
      // response includes the auth string; never log it
      var net = await rest.GetAsync("/network/ipv4").ConfigureAwait(false);

      if (id == null || info == null)
      {
        this.LogDebug("Device info not available yet; will retry on next poll");
        return;
      }

      DeviceInfo = new DeviceInfoModel
      {
        IpAddress = config.Host,
        HostName = net != null ? (string)net["hostname"] : null,
        MacAddress = (string)id["mac"],
        SerialNumber = (string)id["barcode"],
        FirmwareVersion = (string)info["fwId"]
      };

      model = (string)id["model"] ?? (string)info["model"] ?? string.Empty;
      Interlocked.Exchange(ref deviceInfoStale, 0);

      this.LogInformation("Device info: model {Model}, serial {Serial}, firmware {Firmware}, API {ApiVer}",
          model, DeviceInfo.SerialNumber, DeviceInfo.FirmwareVersion, (string)info["apiVer"]);

      ModelFeedback.FireUpdate();

      var handler = DeviceInfoChanged;
      if (handler != null) handler(this, new DeviceInfoEventArgs(DeviceInfo));
    }

    #endregion

    #region State application

    private void SetPowerState(string state)
    {
      state = state ?? string.Empty;
      if (state == powerState) return;
      powerState = state;
      this.LogDebug("Power state: {State}", state);
      PowerIsOnFeedback.FireUpdate();
      PowerIsOffFeedback.FireUpdate();
    }

    private void ApplyVolume(JObject vol)
    {
      var lvl = vol["level"];
      if (lvl != null && lvl.Type != JTokenType.Null)
      {
        levelDb = (double)lvl;
        VolumeLevelFeedback.FireUpdate();
      }

      var mute = vol["mute"];
      if (mute != null && mute.Type == JTokenType.Boolean)
      {
        muted = (bool)mute;
        FireMuteFeedbacks();
      }
    }

    private void ApplyInputs(JObject inp)
    {
      var arr = inp["input"] as JArray;
      if (arr == null) return;

      var selected = new HashSet<string>(arr.Select(t => (string)t), StringComparer.OrdinalIgnoreCase);

      var match = inputOrder.FirstOrDefault(i => selected.SetEquals(i.SmartIpInputs));

      inputs.CurrentItem = match != null ? match.Key : string.Empty;
    }

    #endregion

    #region Power

    /// <inheritdoc />
    public void PowerOn()
    {
      // ACTIVE always wakes the device from STANDBY / ISS_SLEEP
      SendPower(StateActive);
    }

    /// <inheritdoc />
    public void PowerOff()
    {
      SendPower(StateStandby);
    }

    /// <inheritdoc />
    public void PowerToggle()
    {
      if (PowerIsOnFeedback.BoolValue) PowerOff(); else PowerOn();
    }

    private void SendPower(string state)
    {
      Task.Run(async () =>
      {
        if (await rest.PutAsync("/device/pwr", new { state }).ConfigureAwait(false))
        {
          SetPowerState(state);
          // give the device a moment, then confirm the real state
          await Task.Delay(1000).ConfigureAwait(false);
          Poll();
        }
      });
    }

    #endregion

    #region Volume / mute

    /// <inheritdoc />
    public void SetVolume(ushort level)
    {
      if (IgnoreForFixedLevel("SetVolume")) return;
      SetLevelDb(GenelecSmartIpVolumeScaling.ToDb(level, config.VolumeMinDb, config.VolumeMaxDb));
    }

    /// <summary>
    /// True (and logged) when fixedLevelDb is set, so volume commands are dropped instead of fighting it
    /// </summary>
    private bool IgnoreForFixedLevel(string command)
    {
      if (!fixedLevelDb.HasValue) return false;
      this.LogDebug("{Command} ignored: level is fixed at {Level} dB (fixedLevelDb)", command, fixedLevelDb.Value);
      return true;
    }

    /// <summary>
    /// Puts the speaker back to fixedLevelDb when the polled level differs (or is missing)
    /// </summary>
    private async Task EnforceFixedLevelAsync(JObject vol, double target)
    {
      var lvl = vol["level"];
      var current = lvl != null && lvl.Type != JTokenType.Null ? (double?)(double)lvl : null;
      if (current.HasValue && !GenelecSmartIpVolumeScaling.NeedsFixedLevel(current.Value, target)) return;

      this.LogInformation("Applying fixed level {Level} dB (speaker reported {Current})",
          target, current.HasValue ? current.Value.ToString("0.0") : "no level");

      if (await rest.PutAsync("/audio/volume", new { level = target }).ConfigureAwait(false))
      {
        levelDb = target;
        VolumeLevelFeedback.FireUpdate();
      }
      // on failure the next poll tries again
    }

    private void SetLevelDb(double db)
    {
      db = GenelecSmartIpVolumeScaling.Clamp(db, config.VolumeMinDb, config.VolumeMaxDb);
      levelDb = db;
      VolumeLevelFeedback.FireUpdate();
      Task.Run(() => rest.PutAsync("/audio/volume", new { level = db }));
    }

    /// <inheritdoc />
    public void VolumeUp(bool pressRelease)
    {
      Ramp(pressRelease, config.VolumeStepDb);
    }

    /// <inheritdoc />
    public void VolumeDown(bool pressRelease)
    {
      Ramp(pressRelease, -config.VolumeStepDb);
    }

    private void Ramp(bool press, double stepDb)
    {
      lock (stateLock)
      {
        StopRamp();
        if (!press) return;
        if (IgnoreForFixedLevel(stepDb > 0 ? "VolumeUp" : "VolumeDown")) return;

        Interlocked.Exchange(ref rampSteps, 1);
        rampTimer = new Timer(_ =>
        {
          // safety: never ramp forever if the release is lost
          if (Interlocked.Increment(ref rampSteps) > MaxRampSteps)
          {
            lock (stateLock) StopRamp();
            return;
          }
          SetLevelDb(levelDb + stepDb);
        }, null, 0, Math.Max(100, config.VolumeRampIntervalMs));
      }
    }

    private void StopRamp()
    {
      if (rampTimer != null)
      {
        rampTimer.Dispose();
        rampTimer = null;
      }
      Interlocked.Exchange(ref rampSteps, 0);
    }

    /// <inheritdoc />
    public void MuteOn()
    {
      SetMute(true);
    }

    /// <inheritdoc />
    public void MuteOff()
    {
      SetMute(false);
    }

    /// <inheritdoc />
    public void MuteToggle()
    {
      SetMute(!muted);
    }

    private void SetMute(bool mute)
    {
      muted = mute;
      FireMuteFeedbacks();
      Task.Run(() => rest.PutAsync("/audio/volume", new { mute }));
    }

    private void FireMuteFeedbacks()
    {
      MuteFeedback.FireUpdate();
      MuteIsOffFeedback.FireUpdate();
    }

    #endregion

    #region Inputs

    private void BuildInputs()
    {
      var cfg = config.Inputs != null && config.Inputs.Count > 0
          ? config.Inputs
          : GenelecSmartIpPropertiesConfig.DefaultInputs();

      foreach (var kvp in cfg)
      {
        if (kvp.Value == null || kvp.Value.Inputs == null || kvp.Value.Inputs.Count == 0)
        {
          this.LogWarning("Input {Key} has no \"inputs\" configured; skipping", kvp.Key);
          continue;
        }

        var item = new GenelecSmartIpInputItem(
            kvp.Key,
            string.IsNullOrEmpty(kvp.Value.Name) ? kvp.Key : kvp.Value.Name,
            kvp.Value.Inputs,
            SelectInput);

        inputs.Items[kvp.Key] = item;
        inputOrder.Add(item);
      }

      if (inputOrder.Count > GenelecSmartIpBridgeJoinMap.MaxInputs)
      {
        this.LogWarning("{Count} inputs configured; only the first {Max} are available on the bridge",
            inputOrder.Count, GenelecSmartIpBridgeJoinMap.MaxInputs);
      }
    }

    /// <summary>
    /// Selects an input by 1-based position in config order. Out-of-range values are ignored
    /// </summary>
    public void SelectInput(int index)
    {
      if (index < 1 || index > inputOrder.Count) return;
      SelectInput(inputOrder[index - 1]);
    }

    private void SelectInput(GenelecSmartIpInputItem item)
    {
      Task.Run(async () =>
      {
        if (await rest.PutAsync("/audio/inputs", new { input = item.SmartIpInputs }).ConfigureAwait(false))
        {
          inputs.CurrentItem = item.Key;
        }
      });
    }

    #endregion

    #region Bridge

    /// <inheritdoc />
    public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
    {
      var joinMap = new GenelecSmartIpBridgeJoinMap(joinStart);

      if (bridge != null)
      {
        bridge.AddJoinMap(Key, joinMap);
      }

      var customJoins = JoinMapHelper.TryGetJoinMapAdvancedForDevice(joinMapKey);
      if (customJoins != null)
      {
        joinMap.SetCustomJoinData(customJoins);
      }

      this.LogDebug("Linking to Trilist {Id}", trilist.ID.ToString("X"));
      this.LogInformation("Linking to Bridge Type {Type}", GetType().Name);

      // online / comm status
      monitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
      monitor.StatusChange += (s, a) =>
          trilist.UShortInput[joinMap.CommunicationStatus.JoinNumber].UShortValue = (ushort)a.Status;

      // power
      trilist.SetSigTrueAction(joinMap.PowerOn.JoinNumber, PowerOn);
      trilist.SetSigTrueAction(joinMap.PowerOff.JoinNumber, PowerOff);
      trilist.SetSigTrueAction(joinMap.PowerToggle.JoinNumber, PowerToggle);
      PowerIsOnFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PowerOn.JoinNumber]);
      PowerIsOffFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PowerOff.JoinNumber]);

      // volume / mute
      trilist.SetBoolSigAction(joinMap.VolumeUp.JoinNumber, VolumeUp);
      trilist.SetBoolSigAction(joinMap.VolumeDown.JoinNumber, VolumeDown);
      trilist.SetUShortSigAction(joinMap.Volume.JoinNumber, SetVolume);
      VolumeLevelFeedback.LinkInputSig(trilist.UShortInput[joinMap.Volume.JoinNumber]);

      trilist.SetSigTrueAction(joinMap.MuteToggle.JoinNumber, MuteToggle);
      trilist.SetSigTrueAction(joinMap.MuteOn.JoinNumber, MuteOn);
      trilist.SetSigTrueAction(joinMap.MuteOff.JoinNumber, MuteOff);
      MuteFeedback.LinkInputSig(trilist.BooleanInput[joinMap.MuteToggle.JoinNumber]);
      MuteFeedback.LinkInputSig(trilist.BooleanInput[joinMap.MuteOn.JoinNumber]);
      MuteIsOffFeedback.LinkInputSig(trilist.BooleanInput[joinMap.MuteOff.JoinNumber]);

      // inputs
      trilist.SetUShortSigAction(joinMap.InputSelectIndex.JoinNumber, v => SelectInput((int)v));
      CurrentInputIndexFeedback.LinkInputSig(trilist.UShortInput[joinMap.InputSelectIndex.JoinNumber]);
      CurrentInputNameFeedback.LinkInputSig(trilist.StringInput[joinMap.CurrentInputName.JoinNumber]);

      var inputCount = Math.Min(inputOrder.Count, (int)GenelecSmartIpBridgeJoinMap.MaxInputs);
      for (var i = 0; i < inputCount; i++)
      {
        var item = inputOrder[i];
        var digitalJoin = (uint)(joinMap.InputSelect.JoinNumber + i);

        trilist.SetSigTrueAction(digitalJoin, () => SelectInput(item));
        item.ItemUpdated += (s, e) => trilist.BooleanInput[digitalJoin].BoolValue = item.IsSelected;
      }

      // device info
      ModelFeedback.LinkInputSig(trilist.StringInput[joinMap.Model.JoinNumber]);
      DeviceInfoChanged += (d, a) => SetDeviceInfoJoins(trilist, joinMap);

      UpdateBridge(trilist, joinMap, inputCount);

      trilist.OnlineStatusChange += (o, a) =>
      {
        if (!a.DeviceOnLine) return;
        UpdateBridge(trilist, joinMap, inputCount);
      };
    }

    private void UpdateBridge(BasicTriList trilist, GenelecSmartIpBridgeJoinMap joinMap, int inputCount)
    {
      trilist.SetString(joinMap.DeviceName.JoinNumber, Name);
      trilist.UShortInput[joinMap.CommunicationStatus.JoinNumber].UShortValue = (ushort)monitor.Status;

      for (var i = 0; i < inputCount; i++)
      {
        trilist.SetString((uint)(joinMap.InputNames.JoinNumber + i), inputOrder[i].Name);
        trilist.BooleanInput[(uint)(joinMap.InputSelect.JoinNumber + i)].BoolValue = inputOrder[i].IsSelected;
      }

      SetDeviceInfoJoins(trilist, joinMap);

      monitor.IsOnlineFeedback.FireUpdate();
      PowerIsOnFeedback.FireUpdate();
      PowerIsOffFeedback.FireUpdate();
      VolumeLevelFeedback.FireUpdate();
      FireMuteFeedbacks();
      CurrentInputIndexFeedback.FireUpdate();
      CurrentInputNameFeedback.FireUpdate();
      ModelFeedback.FireUpdate();
    }

    private void SetDeviceInfoJoins(BasicTriList trilist, GenelecSmartIpBridgeJoinMap joinMap)
    {
      var info = DeviceInfo;
      trilist.SetString(joinMap.SerialNumber.JoinNumber, info.SerialNumber ?? string.Empty);
      trilist.SetString(joinMap.FirmwareVersion.JoinNumber, info.FirmwareVersion ?? string.Empty);
      trilist.SetString(joinMap.MacAddress.JoinNumber, info.MacAddress ?? string.Empty);
      trilist.SetString(joinMap.IpAddress.JoinNumber, info.IpAddress ?? string.Empty);
    }

    #endregion
  }
}
