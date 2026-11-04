![PepperDash Essentials Plugin Logo](/images/essentials-plugin-blue.png)

![PepperDash Essentials](https://img.shields.io/badge/PepperDash%20Essentials-≥%20v2.39.0-blue)
![.NET](https://img.shields.io/badge/.NET%20Framework-4.7.2-512BD4)
![Crestron](https://img.shields.io/badge/Crestron-4--Series-lightgrey)
![License](https://img.shields.io/badge/license-MIT-green)

# Genelec Smart IP Essentials Plugin (c) 2026

> **`maintenance-essentials-v2` branch:** this build targets **Essentials v2** (`net472`, PepperDashEssentials 2.39.0) for systems that have not moved to Essentials v3. New work goes to `main` (Essentials v3, `net8`) first and is ported here when a v2 system needs it.

## License

Provided under MIT license

## Overview

Controls Genelec Smart IP loudspeakers through the public unicast REST API (HTTP/1.1, Basic auth, default port 9000).
Mobile control messengers are created automatically for the supported capabilities, and SIMPL Windows control is available through an EISC bridge (see [Join Map](#join-map)).

| Capability | Essentials interface | Smart IP endpoint(s) |
|---|---|---|
| Volume / mute | `IBasicVolumeWithFeedback` | `GET/PUT /audio/volume` |
| Power / standby | `IHasPowerControlWithFeedback` | `GET/PUT /device/pwr` |
| Input select | `IHasInputs<string>` | `GET/PUT /audio/inputs` |
| Online status | `ICommunicationMonitor` | driven by poll results |
| Device info | `IDeviceInfoProvider` | `GET /device/id`, `/device/info`, `/network/ipv4` (read once while ACTIVE, re-read after comms loss) |
| SIMPL bridge | `IBridgeAdvanced` | |

Two ways to deploy: control each speaker directly (`genelecSmartIp`), or control them as one group through the multicast API (`genelecSmartIpMulticastGroup`, see [Multicast group](#multicast-group-genelecsmartipmulticastgroup)).

Not implemented: mDNS discovery, profiles, LED, diagnostics (`/events`), network configuration writes, multicast profile selection.

Notes from the API guide:
* The speaker allows 4 simultaneous connections. The plugin serializes requests and sends `Connection: close`.
* Do not use Smart IP Manager/Configurator at the same time as API control.
* In STANDBY / ISS_SLEEP the speaker does not answer all requests; only `/device/pwr` is polled while not ACTIVE. Sending `ACTIVE` always wakes it.

## Configuration

Device type: `genelecSmartIp`

```json
{
  "key": "speaker-1",
  "name": "Lobby Speaker",
  "type": "genelecSmartIp",
  "group": "audio",
  "properties": {
    "host": "192.168.0.79",
    "port": 9000,
    "username": "admin",
    "password": "admin",
    "pollTimeMs": 5000,
    "requestTimeoutMs": 3000,
    "volumeMinDb": -130,
    "volumeMaxDb": 0,
    "volumeStepDb": 1.0,
    "volumeRampIntervalMs": 250,
    "inputs": {
      "analog": { "name": "Analog", "inputs": [ "A" ] },
      "aoip": { "name": "AoIP 1+2", "inputs": [ "AoIP01", "AoIP02" ] }
    }
  }
}
```

| Property | Default | Notes |
|---|---|---|
| `host` | required | IP/hostname |
| `port` | 9000 | |
| `username` / `password` | admin / admin | HTTP Basic |
| `pollTimeMs` | 5000 | Keep polling infrequent per the API guide |
| `requestTimeoutMs` | 3000 | |
| `volumeMinDb` / `volumeMaxDb` | -130 / 0 | dB range mapped linearly to 0-65535 for `SetVolume` and feedback |
| `volumeStepDb` | 1.0 | dB per VolumeUp/Down step |
| `volumeRampIntervalMs` | 250 | repeat rate while held (auto-stops after 200 steps) |
| `inputs` | analog, aoip1, aoip2, aoip1and2 | Items map to a set of `A` / `AoIP01` / `AoIP02` |
| `fixedLevelDb` | not set (optional) | Fixed "set and forget" level, -200.0 to 0.0 dB. See below |
| `fixedInput` | not set (optional) | Key of one `inputs` item the speaker always plays. See below |

### Fixed level (`fixedLevelDb`)

Optional. Leave it out and volume works normally. Set it when the speaker should always play at
one level and room volume is controlled somewhere else, for example a Cisco codec feeding the
speaker's analog input:

```json
"properties": {
  "host": "10.0.0.50",
  "fixedLevelDb": -12
}
```

When it is set:

* **Enforced on every poll:** whenever the speaker is `ACTIVE` and its level differs from
  `fixedLevelDb`, the plugin sets it back. That covers startup, coming back after a comms loss,
  waking from standby, and a change made at the speaker or in Smart IP Manager. Correction is
  attempted on the next successful ACTIVE poll; request latency and failures can delay it.
* **Volume commands ignored:** `SetVolume`, `VolumeUp` and `VolumeDown` from the bridge or Mobile
  Control are dropped, with a debug log line. Mute still works.
* **Bad values ignored:** a value outside -200.0 to 0.0 dB is logged as a warning and ignored, so
  volume stays under normal control.

### Fixed input (`fixedInput`)

Optional. Leave it out and inputs work normally. The speaker plays whichever of its inputs (`A`,
`AoIP01`, `AoIP02`) are selected on the speaker itself, and the plugin only changes that when told
to. Set `fixedInput` to the key of one of your `inputs` items to make sure each speaker always plays
the right channel, for example after a speaker swap or factory reset:

```json
"properties": {
  "host": "10.0.0.50",
  "fixedInput": "danteLeft",
  "inputs": {
    "danteLeft": { "name": "Dante Left", "inputs": [ "AoIP01" ] }
  }
}
```

When it is set:

* **Enforced on every poll:** whenever the speaker is `ACTIVE` and playing a different set of
  inputs, the plugin selects the fixed item again. Order and case don't matter: `AoIP02`+`AoIP01`
  matches an item of `AoIP01`+`AoIP02`. Like `fixedLevelDb`, correction happens on the next
  successful ACTIVE poll.
* **Input-select commands ignored:** input selects from the bridge or Mobile Control are dropped,
  with a debug log line.
* **Unknown keys ignored:** `fixedInput` is an item *key* (`danteLeft`), not an input name
  (`AoIP01`). A key that names no item is logged as a warning and ignored, so inputs stay under
  normal control.
* **Invalid input names ignored:** the fixed item's `inputs` must use the API's names `A`, `AoIP01`
  and `AoIP02` (any case, no surrounding spaces). An item with anything else, e.g. the typo `AoIP1`, is logged as one
  warning at startup and ignored. The plugin never sends it.
* **Default keys:** when `inputs` is omitted, the default items `analog`, `aoip1`, `aoip2` and
  `aoip1and2` are valid `fixedInput` values.
* **Quiet retries:** the first correction after the speaker was on the right input logs at
  Information. If it keeps differing (e.g. the speaker accepts the PUT but doesn't change), repeat
  attempts log at Debug.

`fixedInput` and `fixedLevelDb` are independent: use either or both.

## Join Map

Bridge the device with an `eiscApiAdvanced` bridge entry. Inputs are numbered in the order they appear in `inputs` config (max 10 on the bridge).

With `fixedInput` set, the input-select joins (digitals 11-20 and analog 2 from SIMPL) are ignored, but their feedback still reports the speaker's current input. With `fixedLevelDb` set, the volume joins (digitals 5-6 and analog 1 from SIMPL) are ignored the same way, and volume feedback still reports the level.

### Digitals

| Join | Type (RW) | Description |
|---|---|---|
| 1 | R | Is Online |
| 2 | RW | Power On (ACTIVE) / Power Is On feedback |
| 3 | RW | Power Off (STANDBY) / Power Is Off feedback |
| 4 | W | Power Toggle |
| 5 | W | Volume Up (ramps while held) |
| 6 | W | Volume Down (ramps while held) |
| 7 | RW | Mute Toggle / Mute Is On feedback |
| 8 | RW | Mute On / Mute Is On feedback |
| 9 | RW | Mute Off / Mute Is Off feedback |
| 11-20 | RW | Input Select (config order) / Input Is Selected feedback |

### Analogs

| Join | Type (RW) | Description |
|---|---|---|
| 1 | RW | Volume level (0-65535 over `volumeMinDb`..`volumeMaxDb`) |
| 2 | RW | Input select by 1-based index / current index (0 = none or unmatched) |
| 3 | R | Communication status (0 Unknown, 1 Ok, 2 Warning, 3 Error) |

### Serials

| Join | Type (RW) | Description |
|---|---|---|
| 1 | R | Device Name |
| 2 | R | Current Input Name |
| 3 | R | Model |
| 4 | R | Serial Number (barcode) |
| 5 | R | Firmware Version |
| 6 | R | MAC Address |
| 7 | R | IP Address / host |
| 11-20 | R | Input Names (config order) |

### Bridge example

```json
{
  "key": "speaker-bridge",
  "uid": 4,
  "name": "Speaker Bridge",
  "group": "api",
  "type": "eiscApiAdvanced",
  "properties": {
    "control": { "tcpSshProperties": { "address": "127.0.0.2", "port": 0 }, "ipid": "B1", "method": "ipidTcp" },
    "devices": [ { "deviceKey": "speaker-1", "joinStart": 1 } ]
  }
}
```

## Minimum Essentials Framework Versions

* 2.39.0 (this branch, Essentials v2)
* 3.0.0 (`main`, Essentials v3)

## Multicast group (`genelecSmartIpMulticastGroup`)

Controls several speakers as one group with the Smart IP **multicast** API (API doc sections 5-6): one UDP
message reaches every speaker listening on the group's address. Use it as the room's volume device
(`defaultAudioKey`) when one control should drive all the speakers.

It uses **two multicast groups**, so a fixed-level speaker can follow mute without following volume:

| Group | Gets | Typical members |
|---|---|---|
| `volumeGroup` (required) | level, mute, power | Front Left, Front Right |
| `muteGroup` (optional) | mute, power | Subwoofer kept at a fixed level |

Each speaker listens on exactly one multicast address and port (its `volIp` / `volPort`), so a speaker in the
mute group never receives level messages.

### Setup on the speakers
In Smart IP Manager, give each speaker the multicast address and port of its group, e.g. Front Left and
Front Right `239.10.10.1:49152`, Subwoofer `239.10.10.2:49153`. Use an address in 224.0.0.0-239.255.255.255
and a port in 49152-65535. The network must pass multicast between the processor and the speakers (same
network segment, IGMP snooping with a querier on the switches).

### Config
Keep a `genelecSmartIp` device for every speaker: the multicast API has no replies, so these devices provide
feedback, online status and repair. Then add the group:

```json
{
  "key": "room-audio", "name": "Room Audio", "type": "genelecSmartIpMulticastGroup", "group": "audio",
  "properties": {
    "volumeGroup": { "address": "239.10.10.1", "port": 49152, "members": [ "genelec-front-left", "genelec-front-right" ] },
    "muteGroup":   { "address": "239.10.10.2", "port": 49153, "members": [ "genelec-subwoofer" ] },
    "volumeMinDb": -60,
    "volumeMaxDb": 0,
    "volumeStepDb": 1.0,
    "volumeRampIntervalMs": 250,
    "localAddress": "",
    "sendCount": 2,
    "repairDelayMs": 6000
  }
}
```

| Property | Default | Notes |
|---|---|---|
| `volumeGroup` | required | `address`, `port`, `members` (keys of `genelecSmartIp` devices on this group) |
| `muteGroup` | not set | Same shape. Mute and power only |
| `volumeMinDb` / `volumeMaxDb` | -60 / 0 | dB range mapped to the group's 0-65535 volume; clamped to the multicast range -130..0 |
| `volumeStepDb` / `volumeRampIntervalMs` | 1.0 / 250 | Volume up/down ramp |
| `localAddress` | OS chooses | Processor interface to send from. On a processor with a LAN and a control subnet, set the LAN address so the multicast leaves on the speakers' network |
| `sendCount` | 2 | Times each message is sent (20 ms apart). Multicast has no acknowledgement; the messages are idempotent |
| `repairDelayMs` | 6000 | Wait after a command before comparing members' polled values with the group (one 5 s poll plus margin) |

Members: don't give a `volumeGroup` member `fixedLevelDb` (it would put its level back after every change;
a warning is logged). A `muteGroup` member keeps `fixedLevelDb`, which is the point of the mute group.

### Behaviour
* **Volume:** `{"mcast":{"ver":1,"level":-30.0}}` to the volume group. Volume up/down ramp at group level.
* **Mute:** `{"mcast":{"ver":1,"mute":true}}` to both groups.
* **Power:** off sends `STANDBY`, on sends `BOOT` (multicast has no ACTIVE; the speakers reboot to wake, up to a minute), to both groups.
* **Startup:** level and mute are learned from the first volume member's own reports; nothing is sent from a
  placeholder. Until then the group's feedback shows that speaker's current value, set/on/off commands work,
  and volume up/down and mute toggle are ignored.
* **Repair:** once a command has settled, members' polled values are compared with the group every 2 s. A
  member that missed a message (lost datagram) gets it re-sent to its group. Members offline or in standby are
  skipped.
* **Commissioning check:** after reading each member's device info, the group warns once if the speaker's
  `volIp` / `volPort` don't match the group it's listed in, or if multicast is disabled on it (`0.0.0.0`).

Limits: the multicast API itself returns nothing, so feedback is only as fresh as the members' polling
(`pollTimeMs`). There's no SIMPL bridge for the group yet.

## Supported Types

* `genelecSmartIp`: one speaker, unicast REST API
* `genelecSmartIpMulticastGroup`: several speakers as one group, multicast API

## Building

```
dotnet build src/epi-genelec-smart-ip.4Series.csproj
```

Output: `output/PepperDash.Essentials.Plugins.Genelec.Smart.Ip.<version>.cplz`

## Git Hooks (Husky.Net)

See the template documentation: a `pre-commit` hook runs `.github/scripts/Update-ReadmeBadges.ps1` to keep the `.NET` and `PepperDash Essentials` badges in sync. Requires PowerShell 7 (`pwsh`) and the .NET SDK. The hook is installed on restore once the folder is a git repository.
