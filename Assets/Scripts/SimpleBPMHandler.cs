using UnityEngine;

/// <summary>
/// Simple BPM change handler that doesn't modify conductor timing
/// Just allows BPM changes to happen naturally without compensation
/// </summary>
public class SimpleBPMHandler : MonoBehaviour
{
    [Header("BPM Change Settings")]
    // Debug logging removed for performance
    
    private TempoController _tempoController;
    private float _lastBPM;
    
    void Start()
    {
        _tempoController = FindFirstObjectByType<TempoController>();
        if (_tempoController != null)
        {
            _tempoController.OnBpmChanged += OnBPMChanged;
            _lastBPM = _tempoController.CurrentBpm;
        }
    }
    
    void OnDestroy()
    {
        if (_tempoController != null)
        {
            _tempoController.OnBpmChanged -= OnBPMChanged;
        }
    }
    
    private void OnBPMChanged(float newBPM, int changeAmount)
    {
        _lastBPM = newBPM;
        
        // No timing compensation - let BPM change naturally
        // Notes will move at the new speed, which is the expected behavior
    }
}
