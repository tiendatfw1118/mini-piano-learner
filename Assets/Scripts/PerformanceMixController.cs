using UnityEngine;
using UnityEngine.Audio;
using System.Collections;

public sealed class PerformanceMixController : MonoBehaviour
{
    [Header("References")]
    public AudioMixer mixer;                 // Main mixer that contains the music group
    public string musicParamDb = "MusicVol"; // Exposed dB parameter for the music group
    public TempoController tempo;            // Source for streak updates (wired from TempoController)

    [Header("Range (dB)")]
    public float maxDb = 0f;                 // Upper ceiling (loudest allowed)
    public float minDb = -30f;               // Hard floor (absolute minimum)

    [Header("Steps (per event, dB)")]
    public float stepOnHit = +2.5f;          // Per-event increase when not using streak mode
    public float stepOnMiss = -6f;           // Per-event decrease when not using streak mode

    [Header("Streak mode (optional)")]
    public bool useStreakMode = true;        // If true: control by streaks instead of per-event
    public int hitStreakToBoost = 3;         // Hits needed to reach the next loudness goal
    public int missStreakToDuck = 3;         // Misses needed to return toward the floor
    public bool streakProgressive = true;    // Apply fractional steps as streak progresses
    public bool calibrateToMaxAndStart = true; // Ensure threshold maps exactly startDb↔maxDb in N steps
    [Range(0.1f,1.0f)] public float firstHitStepScale = 0.5f; // Scale of the first bump for a gentle start

    [Header("Smoothing (seconds)")]
    public float attackSec = 0.10f;          // Fade time when increasing loudness
    public float releaseSec = 0.10f;         // Fade time when decreasing loudness
    public float extraSecPerDb = 0.03f;      // Extra time per dB change (bigger moves are slower)

    [Header("Init")]
    public bool startDucked = true;          // Start at a low level when the scene loads
    public float startDb = -18f;             // Initial loudness when startDucked is true

    [Header("Debug")]
    public bool debugLogs = false;           // Optional logs for tuning/diagnostics

    float _targetDb;
    Coroutine _co;
    int _lastCorrectStreak = 0;
    int _lastMissStreak = 0;
    int _lastCorrectCapped = 0; // tiến độ đã clamp theo ngưỡng
    int _lastMissCapped = 0;    // tiến độ đã clamp theo ngưỡng

    void Reset()
    {
        if (!tempo) tempo = FindFirstObjectByType<TempoController>(FindObjectsInactive.Exclude);
    }

    void Awake()
    {
        _targetDb = Mathf.Clamp(startDucked ? startDb : maxDb, minDb, maxDb);
        SetImmediate(_targetDb);
        _lastCorrectStreak = 0;
        _lastMissStreak = 0;
        _lastCorrectCapped = 0;
        _lastMissCapped = 0;
    }

    // ===== Public API – direct calls (used only when not in streak mode)
    public void OnJudge(Judge j)
    {
        if (useStreakMode) return; // In streak mode, per-event bumps are ignored
        switch (j)
        {
            case Judge.Perfect:
            case Judge.Great:
            case Judge.Good:
                Bump(stepOnHit, "Judge Hit");
                break;
            case Judge.Miss:
                Bump(stepOnMiss, "Judge Miss");
                break;
        }
    }

    // Main entry point when using streak mode: call after streak counters change
    public void OnStreakChange(int correctStreak, int missStreak)
    {
        if (!useStreakMode) return;

        if (streakProgressive)
        {
            // Áp dụng theo tiến độ: mỗi bước trong streak đóng góp 1/n của step
            if (missStreakToDuck > 0)
            {
                // clamp tiến độ về [0, missStreakToDuck]
                int capped = Mathf.Clamp(missStreak, 0, Mathf.Max(1, missStreakToDuck));
                int inc = Mathf.Max(0, capped - _lastMissCapped);
                if (inc > 0)
                {
                    float perStep = calibrateToMaxAndStart
                        ? (startDb - maxDb) / Mathf.Max(1, missStreakToDuck) // hướng về startDb
                        : (stepOnMiss / Mathf.Max(1, missStreakToDuck));
                    float delta = perStep * inc; // miss không cần scale bước đầu
                    Bump(delta, $"Miss streak prog {capped}/{missStreakToDuck}");
                }
                _lastMissCapped = capped;
            }

            if (hitStreakToBoost > 0)
            {
                int capped = Mathf.Clamp(correctStreak, 0, Mathf.Max(1, hitStreakToBoost));
                int inc = Mathf.Max(0, capped - _lastCorrectCapped);
                if (inc > 0)
                {
                    float perStep = calibrateToMaxAndStart
                        ? (maxDb - startDb) / Mathf.Max(1, hitStreakToBoost) // hướng tới maxDb
                        : (stepOnHit / Mathf.Max(1, hitStreakToBoost));
                    // Nếu đây là lần tăng đầu tiên (từ 0 -> 1), giảm bớt bước để êm hơn
                    bool includesFirstStep = (_lastCorrectCapped == 0);
                    if (includesFirstStep)
                    {
                        // 1 bước đầu scaled, phần còn lại nguyên size
                        float scaledFirst = perStep * Mathf.Clamp01(firstHitStepScale);
                        float remaining = perStep * Mathf.Max(0, inc - 1);
                        float delta = scaledFirst + remaining;
                        Bump(delta, $"Hit streak first+prog {capped}/{hitStreakToBoost}");
                    }
                    else
                    {
                        float delta = perStep * inc;
                        Bump(delta, $"Hit streak prog {capped}/{hitStreakToBoost}");
                    }
                }
                _lastCorrectCapped = capped;
            }
        }
        else
        {
            // Chỉ kích hoạt khi đạt ngưỡng
            if (missStreakToDuck > 0 && missStreak > 0 && (missStreak % missStreakToDuck) == 0)
            {
                float delta = calibrateToMaxAndStart ? (startDb - maxDb) : stepOnMiss;
                Bump(delta, $"Miss streak {missStreak}");
            }

            if (hitStreakToBoost > 0 && correctStreak > 0 && (correctStreak % hitStreakToBoost) == 0)
            {
                float delta = calibrateToMaxAndStart ? (maxDb - startDb) : stepOnHit;
                Bump(delta, $"Hit streak {correctStreak}");
            }
        }

        _lastCorrectStreak = correctStreak;
        _lastMissStreak = missStreak;

        // Reset internal capped progress when streaks reset
        if (correctStreak == 0) _lastCorrectCapped = 0;
        if (missStreak == 0) _lastMissCapped = 0;
    }

    // ===== Core =====
    void Bump(float delta, string reason)
    {
        float prev = _targetDb;
        float lowerBound = calibrateToMaxAndStart ? Mathf.Max(minDb, startDb) : minDb;
        _targetDb = Mathf.Clamp(_targetDb + delta, lowerBound, maxDb);
        float baseDur = _targetDb > prev ? attackSec : releaseSec;
        float dur = baseDur + Mathf.Abs(_targetDb - prev) * Mathf.Max(0f, extraSecPerDb);
        if (debugLogs)
        {
            float currDb;
            mixer.GetFloat(musicParamDb, out currDb);
        }
        StartSmooth(prev, _targetDb, Mathf.Max(0.01f, dur));
    }

    void StartSmooth(float from, float to, float dur)
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoSmooth(from, to, dur));
    }

    IEnumerator CoSmooth(float from, float to, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            float v = Mathf.Lerp(from, to, k);
            mixer.SetFloat(musicParamDb, v);
            yield return null;
        }
        mixer.SetFloat(musicParamDb, to);
    }

    void SetImmediate(float db)
    {
        mixer.SetFloat(musicParamDb, Mathf.Clamp(db, minDb, maxDb));
    }
}
