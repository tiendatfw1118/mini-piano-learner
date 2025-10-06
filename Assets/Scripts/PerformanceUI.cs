using UnityEngine;
using TMPro;
using SpeedItUp.Events;

/// <summary>
/// UI component to display performance statistics and BPM changes
/// </summary>
public class PerformanceUI : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text comboText;
    public TMP_Text bpmText;
    public TMP_Text performanceStatsText;
    public TMP_Text speedChangeText;
    
    [Header("Animation Settings")]
    public float speedChangeDisplayTime = 2f;
    public Color speedUpColor = Color.green;
    public Color speedDownColor = Color.red;
    public Color neutralColor = Color.white;
    
    private TempoController tempoController;
    private Coroutine speedChangeCoroutine;
    
    void Start()
    {
        // Find tempo controller
        tempoController = FindFirstObjectByType<TempoController>();
        
        // Subscribe to events
        GameEventBus.OnComboChanged += OnComboChanged;
        GameEventBus.OnBpmChanged += OnBpmChanged;
        
        // Subscribe to tempo controller events if available
        if (tempoController != null)
        {
            tempoController.OnBpmChanged += OnBpmChangedFromTempo;
        }
        
        // Initialize UI
        UpdateUI();
    }
    
    void OnDestroy()
    {
        // Unsubscribe from events
        GameEventBus.OnComboChanged -= OnComboChanged;
        GameEventBus.OnBpmChanged -= OnBpmChanged;
        
        if (tempoController != null)
        {
            tempoController.OnBpmChanged -= OnBpmChangedFromTempo;
        }
    }
    
    void Update()
    {
        // Update UI every frame for real-time stats
        UpdateUI();
    }
    
    private void UpdateUI()
    {
        if (tempoController == null) return;
        
        // Update combo text
        if (comboText != null)
        {
            comboText.text = $"Combo: {tempoController.CurrentCombo}";
        }
        
        // Update BPM text
        if (bpmText != null)
        {
            bpmText.text = $"BPM: {tempoController.CurrentBpm:F1}";
        }
        
        // Update performance stats
        if (performanceStatsText != null)
        {
            performanceStatsText.text = tempoController.GetPerformanceStats();
        }
    }
    
    private void OnComboChanged(int combo)
    {
        // Combo change is handled in UpdateUI()
    }
    
    private void OnBpmChanged(float newBpm)
    {
        // BPM change is handled in OnBpmChangedFromTempo()
    }
    
    private void OnBpmChangedFromTempo(float newBpm, int change)
    {
        if (speedChangeText == null) return;
        
        // Stop any existing speed change display
        if (speedChangeCoroutine != null)
        {
            StopCoroutine(speedChangeCoroutine);
        }
        
        // Show speed change
        speedChangeCoroutine = StartCoroutine(ShowSpeedChange(change));
    }
    
    private System.Collections.IEnumerator ShowSpeedChange(int change)
    {
        if (speedChangeText == null) yield break;
        
        // Set text and color based on change
        if (change > 0)
        {
            speedChangeText.text = $"⬆️ SPEED UP! +{change} BPM";
            speedChangeText.color = speedUpColor;
        }
        else if (change < 0)
        {
            speedChangeText.text = $"⬇️ SPEED DOWN! {change} BPM";
            speedChangeText.color = speedDownColor;
        }
        else
        {
            speedChangeText.text = "BPM CHANGED";
            speedChangeText.color = neutralColor;
        }
        
        // Make text visible
        speedChangeText.gameObject.SetActive(true);
        
        // Wait for display time
        yield return new WaitForSeconds(speedChangeDisplayTime);
        
        // Hide text
        speedChangeText.gameObject.SetActive(false);
        
        speedChangeCoroutine = null;
    }
    
    /// <summary>
    /// Manually trigger a speed change display (for testing)
    /// </summary>
    [ContextMenu("Test Speed Up")]
    public void TestSpeedUp()
    {
        OnBpmChangedFromTempo(120f, +10);
    }
    
    [ContextMenu("Test Speed Down")]
    public void TestSpeedDown()
    {
        OnBpmChangedFromTempo(100f, -10);
    }
}

