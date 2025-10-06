using UnityEngine;
using UnityEngine.Audio;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// StemController – schedules stems at a common DSP time and optionally gates audio until the first hit.
/// Behavior:
/// - Before the first correct hit, optionally gate all music or just the vocal stem.
/// - After the first hit, control only the vocal stem by streak (miss >= N → mute, hit >= M → unmute).
/// - Uses coroutine tokens for fades (no StopAllCoroutines in Update).
/// </summary>
[DefaultExecutionOrder(50)]
public sealed class StemController : MonoBehaviour
{
    [Header("References")]
    public Conductor conductor;          // Timing anchor (provides dspStartTime / bpm)
    public TempoController tempo;        // Provides CurrentCorrectStreak / CurrentMissStreak

    [Tooltip("Vocal stem (streak-controlled after first hit)")]
    public AudioSource vocalStem;

    [Tooltip("Other musical stems that should always play")]
    public AudioSource[] otherStems;

    [Header("Mixer (recommended)")]
    public AudioMixer mixer;
    [Tooltip("Exposed dB parameter for the VOCAL group (e.g., 'VocalVol')")]
    public string vocalParamDb = "VocalVol";
    public float unmutedDb = 0f;
    public float mutedDb = -80f;

    [Header("Gate-all at start (optional)")]
    [Tooltip("If true, mute ALL stems until first correct hit")]
    public bool gateAllStemsAtStart = true;
    [Tooltip("Exposed dB parameter for the parent 'Music' group (e.g., 'MusicVol')")]
    public string musicParamDb = ""; // Leave empty if you don't gate the parent music group
    public float musicUnmutedDb = 0f;
    public float musicMutedDb = -80f;

    [Header("Fallback (no mixer)")]
    [Range(0f, 1f)] public float vocalUnmutedVolume = 1f;
    [Range(0f, 1f)] public float vocalMutedVolume = 0f;
    [Range(0f, 1f)] public float bandUnmutedVolume = 1f;
    [Range(0f, 1f)] public float bandMutedVolume = 0f;

    [Header("Rules")]
    public int missStreakToMute = 3;
    public int hitStreakToUnmute = 3;

    [Header("Fades")]
    [Tooltip("20–50 ms to avoid clicks")]
    public float fadeMs = 40f;

    [Header("Scheduling")]
    public bool autoSchedule = true;
    public float scheduleLeadSec = 0.10f;
    public bool loopStems = true;

    [Header("Start gating (Approach A)")]
    [Tooltip("Mute đến khi người chơi có hit đầu tiên")]
    public bool startMutedUntilFirstHit = true;
    [Tooltip("Chỉ Approach B mới dùng cờ này (không dùng ở đây)")]
    public bool scheduleOnFirstHit = false; // not used in Approach A

    // --- Internal state ---
    bool _scheduled = false;
    bool _vocalMutedByStreak = false;
    bool _firstHitDone = false;
    bool _gateActive = false;   // true khi đang gate all (trước first hit)

    // Fade coroutine tokens
    Coroutine _gateCo;
    Coroutine _vocalCo;

    // ---------- Unity ----------
    public void Reset()
    {
        if (!conductor) conductor = FindFirstObjectByType<Conductor>(FindObjectsInactive.Exclude);
        if (!tempo) tempo = FindFirstObjectByType<TempoController>(FindObjectsInactive.Exclude);
        _firstHitDone = false;
        _scheduled = false;
        _vocalMutedByStreak = false;
        _gateActive = false;
        _gateCo = null;
        _vocalCo = null;
    }

    void Awake()
    {
        Reset();
        // Pre-mute frame 0 để tránh "bụp"
        if (startMutedUntilFirstHit && gateAllStemsAtStart)
        {
            ApplyMusicGateImmediate(true);
            _gateActive = true;
        }
        else if (startMutedUntilFirstHit && !gateAllStemsAtStart)
        {
            ApplyVocalImmediate(true);
            _vocalMutedByStreak = true;
        }
        else
        {
            ApplyMusicGateImmediate(false);
            ApplyVocalImmediate(false);
        }
    }

    void Update()
    {
        if (autoSchedule && !_scheduled && !scheduleOnFirstHit)
            TrySchedule();

        // Ungate khi có hit đầu tiên
        if (startMutedUntilFirstHit && !_firstHitDone && tempo != null)
        {
            if (tempo.CurrentCorrectStreak > 0)
            {
                _firstHitDone = true;
                if (gateAllStemsAtStart)
                {
                    StartGateFade(false);
                    _vocalMutedByStreak = false;
                    StartVocalFade(false);
                }
                else
                {
                    StartVocalFade(false);
                    _vocalMutedByStreak = false;
                }
            }
        }

        // Sau first hit: chỉ VOCAL theo streak
        if (_firstHitDone && tempo != null)
        {
            if (!_vocalMutedByStreak && tempo.CurrentMissStreak >= missStreakToMute)
            {
                _vocalMutedByStreak = true;
                StartVocalFade(true);
            }
            else if (_vocalMutedByStreak && tempo.CurrentCorrectStreak >= hitStreakToUnmute)
            {
                _vocalMutedByStreak = false;
                StartVocalFade(false);
            }
        }
    }

    // ---------- Public API ----------
    [ContextMenu("Force Schedule")]
    public void TrySchedule()
    {
        if (_scheduled) return;

        double startAt;
        if (conductor != null && conductor.dspStartTime > 0.0)
        {
            // dspStartTime = mốc beat 0 = (scheduleAt + firstBeatOffsetSec) theo Conductor của bạn
            startAt = conductor.dspStartTime - conductor.firstBeatOffsetSec;
            if (startAt <= 0.0) startAt = AudioSettings.dspTime + scheduleLeadSec;
        }
        else
        {
            startAt = AudioSettings.dspTime + scheduleLeadSec;
        }

        if (vocalStem && vocalStem.clip)
        {
            Prepare(vocalStem);
            vocalStem.PlayScheduled(startAt);
        }
        if (otherStems != null)
        {
            foreach (var s in otherStems)
            {
                if (!s || !s.clip) continue;
                Prepare(s);
                s.PlayScheduled(startAt);
            }
        }

        // Gate ngay sau khi schedule
        if (startMutedUntilFirstHit)
        {
            if (gateAllStemsAtStart)
            {
                ApplyMusicGateImmediate(true);
                _gateActive = true;
                _vocalMutedByStreak = false; // gate toàn bộ lúc đầu
            }
            else
            {
                ApplyVocalImmediate(true);
                _vocalMutedByStreak = true;
            }
        }
        else
        {
            ApplyMusicGateImmediate(false);
            ApplyVocalImmediate(false);
            _vocalMutedByStreak = false;
        }

        _firstHitDone = false;
        _scheduled = true;
    }

    public void PauseAll() { if (vocalStem) vocalStem.Pause(); if (otherStems != null) foreach (var s in otherStems) if (s) s.Pause(); }
    public void ResumeAll() { if (vocalStem) vocalStem.UnPause(); if (otherStems != null) foreach (var s in otherStems) if (s) s.UnPause(); }

    public void StopAll(bool immediate = false)
    {
        if (immediate)
        {
            if (vocalStem) vocalStem.Stop();
            if (otherStems != null) foreach (var s in otherStems) if (s) s.Stop();
        }
        else
        {
            // fade out tất cả (vocal + band)
            StartGateFade(true);
        }
        _scheduled = false;
    }

    public void SetBackgroundStem(AudioSource src)
    {
        if (!src) return;
        var list = new List<AudioSource>(otherStems ?? System.Array.Empty<AudioSource>());
        if (!list.Contains(src)) list.Add(src);
        otherStems = list.ToArray();

        if (_scheduled)
        {
            Prepare(src);
            double startAt = (conductor != null && conductor.dspStartTime > 0.0)
                ? conductor.dspStartTime - conductor.firstBeatOffsetSec
                : AudioSettings.dspTime + scheduleLeadSec;
            if (!src.isPlaying) src.PlayScheduled(startAt);
            if (_gateActive) ApplyBandImmediate(true); // áp gate hiện tại
        }
    }

    public void SetVocalStem(AudioSource src)
    {
        if (!src) return;
        vocalStem = src;

        if (_scheduled)
        {
            Prepare(vocalStem);
            double startAt = (conductor != null && conductor.dspStartTime > 0.0)
                ? conductor.dspStartTime - conductor.firstBeatOffsetSec
                : AudioSettings.dspTime + scheduleLeadSec;
            if (!vocalStem.isPlaying) vocalStem.PlayScheduled(startAt);
            if (_gateActive || _vocalMutedByStreak) ApplyVocalImmediate(true);
        }
    }

    // ---------- Helpers ----------
    void Prepare(AudioSource s)
    {
        s.playOnAwake = false;
        s.loop = loopStems;
        s.spatialBlend = 0f;
    }

    // Fade token helpers
    void StartGateFade(bool mute) { if (_gateCo != null) StopCoroutine(_gateCo); _gateCo = StartCoroutine(FadeMusicGate(mute)); }
    void StartVocalFade(bool mute) { if (_vocalCo != null) StopCoroutine(_vocalCo); _vocalCo = StartCoroutine(FadeVocal(mute)); }

    // === GATE (all music) ===
    void ApplyMusicGateImmediate(bool mute)
    {
        if (!gateAllStemsAtStart) return;

        if (mixer && !string.IsNullOrEmpty(musicParamDb))
            mixer.SetFloat(musicParamDb, mute ? musicMutedDb : musicUnmutedDb);
        else
        {
            // Fallback volumes
            ApplyVocalImmediate(mute);
            ApplyBandImmediate(mute);
        }
    }

    IEnumerator FadeMusicGate(bool mute)
    {
        if (!gateAllStemsAtStart) yield break;
        _gateActive = mute;
        float dur = Mathf.Max(0f, fadeMs / 1000f);

        if (mixer && !string.IsNullOrEmpty(musicParamDb))
        {
            mixer.GetFloat(musicParamDb, out float from);
            float to = mute ? musicMutedDb : musicUnmutedDb;
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                mixer.SetFloat(musicParamDb, Mathf.Lerp(from, to, k));
                yield return null;
            }
            mixer.SetFloat(musicParamDb, to);
        }
        else
        {
            // Fallback: fade tất cả sources
            float fromV = vocalStem ? vocalStem.volume : vocalUnmutedVolume;
            float toV = mute ? vocalMutedVolume : vocalUnmutedVolume;
            float fromB = bandUnmutedVolume;
            float toB = mute ? bandMutedVolume : bandUnmutedVolume;

            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                if (vocalStem) vocalStem.volume = Mathf.Lerp(fromV, toV, k);
                if (otherStems != null) foreach (var s in otherStems) if (s) s.volume = Mathf.Lerp(fromB, toB, k);
                yield return null;
            }
            if (vocalStem) vocalStem.volume = toV;
            if (otherStems != null) foreach (var s in otherStems) if (s) s.volume = toB;
        }
    }

    void ApplyBandImmediate(bool mute)
    {
        if (otherStems == null) return;
        foreach (var s in otherStems) if (s) s.volume = mute ? bandMutedVolume : bandUnmutedVolume;
    }

    // === VOCAL (streak-controlled) ===
    void ApplyVocalImmediate(bool mute)
    {
        if (mixer && !string.IsNullOrEmpty(vocalParamDb))
            mixer.SetFloat(vocalParamDb, mute ? mutedDb : unmutedDb);
        else if (vocalStem)
            vocalStem.volume = mute ? vocalMutedVolume : vocalUnmutedVolume;
    }

    IEnumerator FadeVocal(bool mute)
    {
        float dur = Mathf.Max(0f, fadeMs / 1000f);

        if (mixer && !string.IsNullOrEmpty(vocalParamDb))
        {
            mixer.GetFloat(vocalParamDb, out float from);
            float to = mute ? mutedDb : unmutedDb;
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                mixer.SetFloat(vocalParamDb, Mathf.Lerp(from, to, k));
                yield return null;
            }
            mixer.SetFloat(vocalParamDb, to);
        }
        else if (vocalStem)
        {
            float from = vocalStem.volume;
            float to = mute ? vocalMutedVolume : vocalUnmutedVolume;
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                vocalStem.volume = Mathf.Lerp(from, to, k);
                yield return null;
            }
            vocalStem.volume = to;
        }
    }
}
