using System;
using UnityEngine;
using SpeedItUp.Events;

public sealed class TempoController : MonoBehaviour
{
    public Conductor conductor;
    [NonSerialized] public RemoteConfigData cfg;
    public event Action<float,int> OnBpmChanged;
    public PerformanceMixController mix;
    
    // Reference to Boot to check mini-game type
    private Boot _boot;
    
    // Combo tracking
    private int _combo = 0;
    private int _missStreak = 0;
    private int _correctStreak = 0; // Track consecutive correct hits
    
    // Performance tracking
    public int CurrentCombo => _combo;
    public int CurrentMissStreak => _missStreak;
    public int CurrentCorrectStreak => _correctStreak;
    public float CurrentBpm => conductor ? conductor.bpm : 0f;

    public void Setup(RemoteConfigData c) 
    { 
        cfg = c; 
        // Find Boot component to check mini-game type
        _boot = FindFirstObjectByType<Boot>();
        if (!mix) mix = FindFirstObjectByType<PerformanceMixController>(FindObjectsInactive.Exclude);
    }

    public void OnJudge(Judge j)
    {
        if (cfg == null || conductor == null) return;
        
        // Update streaks based on judge result
        if (j == Judge.Miss)
        {
            _missStreak++;
            _combo = 0;
            _correctStreak = 0;
        }
        else
        {
            // Any non-miss result (Perfect, Great, Good) counts as correct
            _combo++;
            _correctStreak++;
            _missStreak = 0;
        }

        // Notify performance mixer about streak changes (used when useStreakMode=true)
        if (mix != null) mix.OnStreakChange(_correctStreak, _missStreak);

        // Only apply BPM changes for TempoIncreasing mini-game
        if (IsTempoGameActive())
        {
            // Check for speed increase: consecutive correct keys
            if (_correctStreak > 0 && _correctStreak % cfg.speedUpCombo == 0)
            {
                IncreaseSpeed();
                _correctStreak = 0; // Reset after speed increase
            }
            
            // Check for speed decrease: consecutive incorrect keys
            if (_missStreak >= cfg.speedDownMissStreak)
            {
                DecreaseSpeed();
                _missStreak = 0; // Reset after speed decrease
            }
        }
        
        // Publish combo change event
        GameEventBus.PublishComboChanged(_combo);
    }
    
/// <summary>
/// Increase BPM by the configured step (TempoIncreasing mini-game only)
/// </summary>
    private void IncreaseSpeed()
    {
        if (cfg == null || conductor == null) return;
        
        int bpmIncrease = cfg.bpmStep;
        float newBpm = Mathf.Min(cfg.maxBpm, conductor.bpm + bpmIncrease);
        
        if (Math.Abs(newBpm - conductor.bpm) > 0.01f)
        {
            conductor.SetBpm(newBpm);
            OnBpmChanged?.Invoke(newBpm, +bpmIncrease);
            GameEventBus.PublishBpmChanged(newBpm);
        }
        else
        {
        }
    }
    
/// <summary>
/// Decrease BPM by the configured step (TempoIncreasing mini-game only)
/// </summary>
    private void DecreaseSpeed()
    {
        if (cfg == null || conductor == null) return;
        
        int bpmDecrease = cfg.bpmStep;
        float newBpm = Mathf.Max(cfg.minBpm, conductor.bpm - bpmDecrease);
        
        if (Math.Abs(newBpm - conductor.bpm) > 0.01f)
        {
            conductor.SetBpm(newBpm);
            OnBpmChanged?.Invoke(newBpm, -bpmDecrease);
            GameEventBus.PublishBpmChanged(newBpm);
            
        }
        else
        {
        }
    }
    
/// <summary>
/// Reset all performance tracking (combo and streaks)
/// </summary>
    public void ResetTracking()
    {
        _combo = 0;
        _missStreak = 0;
        _correctStreak = 0;
    }
    
/// <summary>
/// Reset the tempo controller to initial state (tracking + BPM)
/// </summary>
    public void Reset()
    {
        ResetTracking();
        
        // Reset BPM to initial value if available
        if (cfg != null && conductor != null)
        {
            conductor.bpm = cfg.baseBpm;
        }
    }
    
/// <summary>
/// Get human-readable performance statistics for UI/debugging
/// </summary>
    public string GetPerformanceStats()
    {
        return $"Combo: {_combo} | Correct Streak: {_correctStreak} | Miss Streak: {_missStreak} | BPM: {CurrentBpm:F1}";
    }
    
/// <summary>
/// Check if the TempoIncreasing mini-game is currently active
/// </summary>
    private bool IsTempoGameActive()
    {
        if (_boot == null)
        {
            // If no Boot reference, assume tempo game is active (backward compatibility)
            return true;
        }
        
        return _boot.selectedMiniGame == MiniGameType.TempoIncreasing;
    }
}
