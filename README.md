## Speed It Up – Rhythm Mini-Games (Unity)

This repository implements two Duolingo Music–style mini-games in Unity, optimized for 60 FPS, low input latency, CDN-configurable gameplay, and JSON event logging.

- "Speed it Up" (Rhythm Tap / Tempo Increasing)
- "Perform the Song" (Performance Mode with background stem control)

### Highlights
- Modular systems: `Boot`, `Conductor`, `TempoController`, `JudgeController`, `NoteSpawner`, `PerformanceMixController`, `StemController`, `GameEventBus`.
- Object pooling for notes, lean UI rendering, and horizontal playfield.
- Event bus decoupling for `note_hit`, `note_miss`, `song_end`.
- Remote-configurable BPM, hit windows, input offset via CDN or local StreamingAssets without rebuild.
- JSON console logging for analytics pipelines.


## Mini-Games

### 1) Speed it Up (Rhythm Tap)
- Tap notes in order as they cross the hit line.
- Difficulty increases with tempo: after every 4 consecutive correct hits, BPM +10.
- On 3 consecutive misses, BPM −10.
- BPM changes are applied smoothly by `Conductor` to avoid visual/audio discontinuities.
- Play Scene: SpeedItUp.asset

### 2) Perform the Song (Performance Mode)
- Plays the song at base tempo.
- Background stem audio is dynamically adjusted:
  - First hit starts the stem at silent volume.
  - 3 consecutive hits: unmute to full volume.
  - 3 consecutive misses: mute to silent volume.
- Streak-based and progressive volume control handled by `PerformanceMixController`.
- Play Scene: PerformTheSong.asset


## Architecture Overview

- `Boot` – entry point; selects mini-game, loads RemoteConfig (CDN/local), loads chart JSON, wires systems, provides restart.
- `Conductor` – master clock. Maintains BPM, supports smooth tempo changes, anchors beat to DSP time, starts/stops audio.
- `TempoController` – combo/streak tracking and BPM changes (+/− 10) for the Tempo Increasing game only.
- `JudgeController` – input judging and note lifecycle; uses hit windows from RemoteConfig; publishes events to the bus; resets on restart.
- `NoteSpawner` – spawns notes from chart, assigns lane colors, time-based miss detection, object pooling, and song end detection.
- `NoteView` / `HoldNoteView` – visuals, lane-based coloring, readable in-note text.
- `PerformanceMixController` – streak-driven, progressive audio mixing (smooth fades, bounded ranges, first-hit scaling).
- `StemController` – background stem playback logic (mute/unmute on streaks, start on first hit, reset on restart).
- `GameEventBus` – centralized events; static events initialized with empty delegates to avoid nulls.


## Performance Targets
- FPS ≥ 60
- Hit-latency ≤ 50 ms (hit windows and input offset tunable via RemoteConfig)
- Minimal runtime logging in hot loops; only essential JSON logs are emitted.


## Remote Config (CDN or Local)

`RemoteConfigLoader` loads `RemoteConfig.json` from a CDN (if configured) with fallback to `Assets/StreamingAssets/RemoteConfig.json` or defaults. No rebuild needed for tuning.

Schema (example):
```json
{
  "baseBpm": 100,
  "bpmStep": 10,
  "speedUpCombo": 4,
  "speedDownMissStreak": 3,
  "minBpm": 60,
  "maxBpm": 200,
  "hitWindowMs": { "perfect": 250, "great": 500, "good": 1000 },
  "inputOffsetMs": 100,
  "audioLatencyMs": 0
}
```

Usage:
- `Boot.takeBpmFromRemoteConfig = true` ensures base BPM is sourced from RemoteConfig, not the chart.
- Hit windows and `inputOffsetMs` are read from RemoteConfig and used by judging.

CDN setup options:
- Configure at runtime via `CDNSetup` component (inspector or context menu) or call `RemoteConfigLoader.ConfigureCDN(baseUrl, endpoint, timeout)`. The loader logs errors on failures and falls back to local.
- Currently Used CDN URL: https://world-streamer.s3.ap-southeast-1.amazonaws.com/RemoteConfig_CDN.json


## Charts

- JSON chart files in `Assets/StreamingAssets/Charts/` define: `bpm`, `firstBeatOffsetSec`, and a list of notes with `beat`, `lane`, `degree`, `len` (hold length in beats).
- With `takeBpmFromRemoteConfig = true`, the runtime BPM uses RemoteConfig; chart BPM is used only if the flag is false.


## JSON Console Logging

Emitted by `RhythmLogger`:
- `note_hit`, `note_miss`, `song_end`.

Example:
```json
{"type":"note_hit","noteId":"abc123","songId":"canon","expected":12.345,"input":12.360,"deltaMs":15.0}
{"type":"note_miss","noteId":"abc123","songId":"canon","expected":12.345,"input":12.500,"deltaMs":155.0}
{"type":"song_end","songId":"canon","hit":120,"miss":8,"duration":164.0}
```


## Building and Running

1) Open the project in Unity 6000.1.8f (or newer LTS recommended).
2) Use the scene builder or open the main scene created by `Assets/Editor/SpeedItUpSceneBuilderV21.cs`.
3) In `Boot`:
   - Select scene: SpeedItUp or PerformTheSong.
4) Press Play.


## Design & Code Quality

- Clear separation of concerns, high-readability code, minimal side effects.
- Event-driven architecture via `GameEventBus` to decouple judge, tempo, mix, and UI.
- Progressive audio mixing to avoid sudden jumps at streak thresholds.
- Robust restart flow resetting conductor, tempo, judge, spawner, and stem.
- Defensive coding: static events pre-initialized, null-safe checks, conservative fallbacks.


## Deliverables

- Source code in this repository
- README (this file)


## Troubleshooting

- BPM seems to revert to chart: ensure `Boot.takeBpmFromRemoteConfig = true` and `NoteSpawner` does not override `Conductor.bpm` (it does not in this repo).
- CDN not applied: verify `CDNSetup` configured with a valid URL; check console errors; confirm local fallback file exists.
- Stem never unmutes: confirm `StemController` is present and subscribed, and `PerformanceMixController` streak thresholds are set (> 0).
- Input feels offset: tune `inputOffsetMs` in RemoteConfig; verify hit windows and BPM are correct.

## Known Bugs
- When the BPM got increase, note judging logic will be messed, need investigating.

## TODO
- Apply more than two stems to test for drifting and test StemController usage.


## License

For test/review use.


