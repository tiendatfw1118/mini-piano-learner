using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using SpeedItUp.Events;
using SpeedItUp.Input;

/// <summary>
/// TapInput that integrates with the new design pattern architecture
/// This is a compatibility layer that bridges the old TapInput interface with the new system
/// </summary>
public sealed class TapInput : MonoBehaviour
{
    public System.Action<double> OnTapDsp;
    public System.Action<int, double> OnTapDegreeDsp;
    public System.Action<int, double> OnDegreeDownDsp;
    public System.Action<int, double> OnDegreeUpDsp;

    private bool[] held = new bool[8];
    public bool IsHeld(int degree) 
    {
        return degree >= 1 && degree <= 7 && held[degree];
    }
    
    [Header("Debug Options")]
    public bool debugInput = false;

    // Reference to the improved input manager
    private ImprovedInputManager improvedInputManager;

    void Start()
    {
        // Find or create the improved input manager
        improvedInputManager = FindFirstObjectByType<ImprovedInputManager>();
        if (improvedInputManager == null)
        {
            var go = new GameObject("ImprovedInputManager");
            improvedInputManager = go.AddComponent<ImprovedInputManager>();
        }

        // Subscribe to the improved input manager's events
        improvedInputManager.OnInputEvent += HandleImprovedInputEvent;
    }

    void OnDestroy()
    {
        if (improvedInputManager != null)
        {
            improvedInputManager.OnInputEvent -= HandleImprovedInputEvent;
        }
    }

    void OnEnable() => EnhancedTouchSupport.Enable();
    void OnDisable() => EnhancedTouchSupport.Disable();

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.digit1Key.wasPressedThisFrame) DegreeDown(1);
            if (kb.digit2Key.wasPressedThisFrame) DegreeDown(2);
            if (kb.digit3Key.wasPressedThisFrame) DegreeDown(3);
            if (kb.digit4Key.wasPressedThisFrame) DegreeDown(4);
            if (kb.digit5Key.wasPressedThisFrame) DegreeDown(5);
            if (kb.digit6Key.wasPressedThisFrame) DegreeDown(6);
            if (kb.digit7Key.wasPressedThisFrame) DegreeDown(7);
            if (kb.spaceKey.wasPressedThisFrame) OnTapDsp?.Invoke(AudioSettings.dspTime);

            if (kb.digit1Key.wasReleasedThisFrame) DegreeUp(1);
            if (kb.digit2Key.wasReleasedThisFrame) DegreeUp(2);
            if (kb.digit3Key.wasReleasedThisFrame) DegreeUp(3);
            if (kb.digit4Key.wasReleasedThisFrame) DegreeUp(4);
            if (kb.digit5Key.wasReleasedThisFrame) DegreeUp(5);
            if (kb.digit6Key.wasReleasedThisFrame) DegreeUp(6);
            if (kb.digit7Key.wasReleasedThisFrame) DegreeUp(7);
        }

        if (Touch.activeTouches.Count > 0)
        {
            foreach (var t in Touch.activeTouches)
                if (t.phase == UnityEngine.InputSystem.TouchPhase.Began)
                    OnTapDsp?.Invoke(AudioSettings.dspTime);
        }
    }

    public void RaiseTapDegree(int degree)
    {
        double now = AudioSettings.dspTime;
        OnTapDegreeDsp?.Invoke(Mathf.Clamp(degree, 1, 7), now);
    }

    public void DegreeDown(int degree)
    {
        degree = Mathf.Clamp(degree, 1, 7);
        if (held[degree]) return;
        held[degree] = true;
        double now = AudioSettings.dspTime;
        
        // Debug logging removed for performance
        
        // Only call OnDegreeDownDsp - this handles both regular notes and hold notes
        // OnTapDegreeDsp is redundant and causes double processing
        OnDegreeDownDsp?.Invoke(degree, now);
    }

    public void DegreeUp(int degree)
    {
        degree = Mathf.Clamp(degree, 1, 7);
        if (!held[degree]) return;
        held[degree] = false;
        double now = AudioSettings.dspTime;
        OnDegreeUpDsp?.Invoke(degree, now);
    }

    /// <summary>
    /// Handle input events from the improved input manager
    /// This bridges the new system with the old interface
    /// </summary>
    private void HandleImprovedInputEvent(InputEvent inputEvent)
    {
        // Debug logging removed for performance

        // The improved input manager handles the actual processing
        // This method is here for compatibility and potential future use
    }
}
