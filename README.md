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

The plugin has two device types, which can be used alone or together. See [Ways to deploy](#ways-to-deploy).

Not implemented: mDNS discovery, profiles, LED, diagnostics (`/events`), network configuration writes, multicast profile selection.

Notes from the API guide:
* The speaker allows 4 simultaneous connections. The plugin serializes requests and sends `Connection: close`.
* Do not use Smart IP Manager/Configurator at the same time as API control.
* In STANDBY / ISS_SLEEP the speaker does not answer all requests; only `/device/pwr` is polled while not ACTIVE. Sending `ACTIVE` always wakes it.

## Ways to deploy

| Device type | Talks to | What it is |
|---|---|---|
| `genelecSmartIp` | one speaker, unicast REST (TCP 9000) | Control **and** feedback for one speaker: level, mute, power, input, online status, device info |
| `genelecSmartIpMulticastGroup` | every speaker on a multicast address, UDP | Sends level, mute and power to all of them at once. **Send only:** the multicast API never replies |

The two types are independent. Four common layouts:

| # | Config | Room volume device (`defaultAudioKey`) | Use when |
|---|---|---|---|
| A | `genelecSmartIp` per speaker, no group | one speaker, or something that drives the others (e.g. a codec with followers) | One speaker, or speakers controlled separately |
| B | Group only, `members: []`, no `genelecSmartIp` devices | the group | Simplest one-control room, and no feedback or repair is acceptable |
| C | Group **plus** a `genelecSmartIp` per speaker, listed in `members` | the group | **Recommended** for one-control rooms: one command for all speakers, with feedback, repair and health |
| D | Group plus `genelecSmartIp` devices, only some listed in `members` | the group | Some speakers need their own device (e.g. `fixedInput`, health tab) but shouldn't drive group feedback |

What each layout gives you:

| | A: speakers only | B: group only | C: group + listed speakers | D: mixed |
|---|---|---|---|---|
| One command reaches every speaker | no (one device per speaker) | yes | yes | yes |
| Volume / mute feedback | real, per speaker | last value sent; level shows the top of the range until the first `SetVolume` | real, from the first `volumeGroup` member | real, from the first listed `volumeGroup` member |
| Volume up/down, mute toggle | yes | after the first `SetVolume` / `MuteOn` / `MuteOff` | yes, once the first member reports | as C |
| Power feedback / toggle | yes | always off, so toggle always wakes (`BOOT` reboots the speakers) | yes, from the first `volumeGroup` member | as C |
| Repair of a missed message | n/a (unicast) | no | yes | listed members only |
| Online status, device info, health | yes | no | yes | for each configured speaker |
| Commissioning check (`volIp` / `volPort`) | no | no | yes | listed members only |
| `fixedLevelDb`, `fixedInput` enforced | yes | no (needs a speaker device) | yes | for each configured speaker |
| Mobile Control / SIMPL bridge | per speaker | Mobile Control volume; no SIMPL bridge yet | as A and B | as A and B |

Rules that apply to every layout:
* **The speaker decides what it hears, not the config.** A speaker obeys every multicast message sent to its
  `volIp` / `volPort` (set in Smart IP Manager), whether or not it appears in any `members` list or has a
  `genelecSmartIp` device. `members` only tells the group which devices to read feedback from, repair and check.
* **A `genelecSmartIp` device still works on its own** while it is a group member. Commands sent straight to it
  (from its own bridge or Mobile Control) change only that speaker, and the group repairs the difference
  back a few seconds later.
* **A speaker device can't be the group.** Don't point `defaultAudioKey` at one speaker and expect the others
  to follow over multicast: only the group sends multicast.

### A: speakers only

```json
{ "key": "genelec-front-left",  "type": "genelecSmartIp", "properties": { "host": "10.0.0.51" } },
{ "key": "genelec-front-right", "type": "genelecSmartIp", "properties": { "host": "10.0.0.52" } },
{ "key": "genelec-subwoofer",   "type": "genelecSmartIp", "properties": { "host": "10.0.0.53", "fixedLevelDb": -12 } }
```

Each device controls one speaker. To drive several from one control, something above the plugin has to
send to each of them (for example a room plugin with codec followers).

### B: group only

```json
{
  "key": "room-audio", "type": "genelecSmartIpMulticastGroup",
  "properties": {
    "volumeGroup": { "address": "239.10.10.1", "port": 49152, "members": [] },
    "muteGroup":   { "address": "239.10.10.2", "port": 49153, "members": [] }
  }
}
```

No speaker devices. The group logs a warning at startup that it has no feedback. Send `SetVolume` and
`MuteOn`/`MuteOff` (rather than relying on up/down and toggle) until the first command has set a value. The
sub's level is whatever was last set on it, e.g. in Smart IP Manager, since nothing enforces `fixedLevelDb`.

### C: group plus listed speakers (recommended)

```json
{ "key": "genelec-front-left",  "type": "genelecSmartIp", "properties": { "host": "10.0.0.51" } },
{ "key": "genelec-front-right", "type": "genelecSmartIp", "properties": { "host": "10.0.0.52" } },
{ "key": "genelec-subwoofer",   "type": "genelecSmartIp", "properties": { "host": "10.0.0.53", "fixedLevelDb": -12 } },
{
  "key": "room-audio", "type": "genelecSmartIpMulticastGroup",
  "properties": {
    "volumeGroup": { "address": "239.10.10.1", "port": 49152, "members": [ "genelec-front-left", "genelec-front-right" ] },
    "muteGroup":   { "address": "239.10.10.2", "port": 49153, "members": [ "genelec-subwoofer" ] }
  }
}
```

The room points `defaultAudioKey` at `room-audio`. The speaker devices can also go in the room's
accessory/health list. Startup, feedback and repair are described in [Behaviour](#behaviour).

### D: mixed

Same as C, but a configured speaker is left out of `members`, for example a speaker kept only so its
`fixedInput` is enforced, or one on a different multicast address that this group shouldn't check. It
still hears the group if its `volIp` / `volPort` match, but the group doesn't read its feedback or repair it.
Prefer C unless you have a reason to leave one out.

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
The multicast API has no replies, so feedback, online status and repair come from `genelecSmartIp` devices
listed in `members`. Listing every speaker is recommended (layout C). `members` can also be empty (layout B),
see [Ways to deploy](#ways-to-deploy) for what that gives up. A full example:

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
| `volumeGroup` | required | `address`, `port`, `members` (keys of `genelecSmartIp` devices on this group; optional, may be empty) |
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

### Speakers plus a fixed-level subwoofer: how the two groups work together

**Membership is set on the speakers, not in this config.** A speaker obeys every message sent to the multicast
address and port it listens on (`volIp` / `volPort`, set in Smart IP Manager). It never sees messages sent to
any other address. The `members` lists in the config don't change what a speaker receives. They only tell the
group which `genelecSmartIp` devices to read feedback from, repair, and check.

So a sub that must keep its own level needs **its own address**. If it shared the mains' address it would get
every level message too. `fixedLevelDb` would then put it back to its level on the next poll, so the sub would
jump with every volume change and return a few seconds later.

With Front Left / Front Right on `volumeGroup` and the Subwoofer on `muteGroup`:

| Action | Message | Sent to | Front Left / Front Right | Subwoofer |
|---|---|---|---|---|
| Set volume / ramp | `level` | `volumeGroup` only | change level | unchanged: never receives level |
| Mute / unmute | `mute` | both groups | mute / unmute | mute / unmute |
| Power off / on | `state` `STANDBY` / `BOOT` | both groups | standby / wake | standby / wake |
| Repair (a member missed a message) | the missed value | that member's group | level and/or mute re-sent | mute re-sent |

How the sub's level stays put:
* The sub's own `genelecSmartIp` device holds `fixedLevelDb` (e.g. -12). It sets that level whenever the sub
  is on and reports anything else: at startup, after a reboot, or after a change made in Smart IP Manager.
* The group never sends a level to `muteGroup`, so it never fights `fixedLevelDb`.
* For the mains it's the other way round: don't give a `volumeGroup` member `fixedLevelDb`, or it puts its
  level back after every group change. The group logs a warning if one does.

Example: the room is at -30 dB and the user ramps up to -20 dB, then mutes.
1. During the ramp, level messages go to `239.10.10.1:49152`. Front Left and Front Right follow; the sub stays at -12.
2. Mute: `{"mcast":{"ver":1,"mute":true}}` goes to `239.10.10.1:49152` **and** `239.10.10.2:49153`. All three mute.
3. A few seconds later the members' polls report -20 / muted (sub: -12 / muted). Everything matches, so nothing is re-sent.

Other layouts:
* **No fixed-level speaker:** leave `muteGroup` out and put every speaker on `volumeGroup`.
* **A sub that follows the volume:** put it on `volumeGroup` too, without `fixedLevelDb`. It then moves with
  the mains; any offset between them has to be set on the sub itself (e.g. its profile).
* **Mute-only speaker without `fixedLevelDb`:** it works; its level is just whatever it was last set to, e.g. in
  Smart IP Manager. `fixedLevelDb` makes sure it returns to that level after a reset or speaker swap.

Several rooms on one network:
* Each room's groups need their own addresses, e.g. room A `239.10.10.1` / `.2`, room B `239.10.10.3` / `.4`.
  A shared address means one room's volume or mute reaches the other room's speakers.
* Two rooms on the same address are also not caught by the commissioning check, because each speaker's
  `volIp` matches the group it's listed in.

Limits: the multicast API itself returns nothing, so feedback is only as fresh as the members' polling
(`pollTimeMs`). There's no SIMPL bridge for the group yet.

## Supported Types

* `genelecSmartIp`: one speaker, unicast REST API
* `genelecSmartIpMulticastGroup`: several speakers as one group, multicast API (alone or with `genelecSmartIp` members, see [Ways to deploy](#ways-to-deploy))

## Building

```
dotnet build src/epi-genelec-smart-ip.4Series.csproj
```

Output: `output/PepperDash.Essentials.Plugins.Genelec.Smart.Ip.<version>.cplz`

## Git Hooks (Husky.Net)

See the template documentation: a `pre-commit` hook runs `.github/scripts/Update-ReadmeBadges.ps1` to keep the `.NET` and `PepperDash Essentials` badges in sync. Requires PowerShell 7 (`pwsh`) and the .NET SDK. The hook is installed on restore once the folder is a git repository.
