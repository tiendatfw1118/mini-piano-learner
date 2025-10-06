using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using SpeedItUp.Events;
using SpeedItUp.Input;

/// <summary>
/// PianoInputState that integrates with the new design pattern architecture
/// This provides backward compatibility while using the improved input system
/// </summary>
public enum PianoKeyState
{
    Idle,       // Key is not pressed
    Pressed,    // Key is currently being held down
    Released    // Key was just released
}

public class PianoKey
{
    public int degree;
    public PianoKeyState state;
    public double lastPressTime;
    public double lastReleaseTime;
    public bool hasBeenProcessed; // Prevents double processing
    
    public PianoKey(int degree)
    {
        this.degree = degree;
        this.state = PianoKeyState.Idle;
        this.lastPressTime = 0;
        this.lastReleaseTime = 0;
        this.hasBeenProcessed = false;
    }
}

public class PianoInputState : MonoBehaviour
{
    [Header("Debug Options")]
    public bool debugStateChanges = false;
    
    private Dictionary<int, PianoKey> pianoKeys;
    private JudgeController judgeController;
    private ImprovedInputManager improvedInputManager;
    
    // Events for state changes (backward compatibility)
    public System.Action<int, double> OnKeyPressed;
    public System.Action<int, double> OnKeyReleased;
    public System.Action<int, double> OnKeyTapped; // Single tap (press + release)
    
    void Start()
    {
        InitializePianoKeys();
        judgeController = FindFirstObjectByType<JudgeController>();
        
        // Find or create the improved input manager
        improvedInputManager = FindFirstObjectByType<ImprovedInputManager>();
        if (improvedInputManager == null)
        {
            var go = new GameObject("ImprovedInputManager");
            improvedInputManager = go.AddComponent<ImprovedInputManager>();
        }

        // Subscribe to the improved input manager's events
        improvedInputManager.OnInputEvent += HandleImprovedInputEvent;
        
        if (judgeController != null)
        {
            // REMOVED: OnKeyTapped - not needed since all notes are hold notes
        }
    }

    void OnDestroy()
    {
        if (improvedInputManager != null)
        {
            improvedInputManager.OnInputEvent -= HandleImprovedInputEvent;
        }
    }
    
    void InitializePianoKeys()
    {
        pianoKeys = new Dictionary<int, PianoKey>();
        for (int i = 1; i <= 7; i++)
        {
            pianoKeys[i] = new PianoKey(i);
        }
    }
    
    void Update()
    {
        HandleKeyboardInput();
        UpdateKeyStates();
    }
    
    void HandleKeyboardInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        
        // Check each piano key (1-7)
        for (int degree = 1; degree <= 7; degree++)
        {
            var key = pianoKeys[degree];
            bool isPressed = IsKeyPressed(degree, kb);
            
            switch (key.state)
            {
                case PianoKeyState.Idle:
                    if (isPressed)
                    {
                        HandleKeyPress(degree);
                    }
                    break;
                    
                case PianoKeyState.Pressed:
                    if (!isPressed)
                    {
                        HandleKeyRelease(degree);
                    }
                    break;
                    
                case PianoKeyState.Released:
                    // Transition back to Idle after a short delay
                    if (Time.time - key.lastReleaseTime > 0.1f)
                    {
                        key.state = PianoKeyState.Idle;
                        key.hasBeenProcessed = false;
                    }
                    break;
            }
        }
    }
    
    bool IsKeyPressed(int degree, Keyboard kb)
    {
        switch (degree)
        {
            case 1: return kb.digit1Key.isPressed;
            case 2: return kb.digit2Key.isPressed;
            case 3: return kb.digit3Key.isPressed;
            case 4: return kb.digit4Key.isPressed;
            case 5: return kb.digit5Key.isPressed;
            case 6: return kb.digit6Key.isPressed;
            case 7: return kb.digit7Key.isPressed;
            default: return false;
        }
    }
    
    void HandleKeyPress(int degree)
    {
        var key = pianoKeys[degree];
        key.state = PianoKeyState.Pressed;
        key.lastPressTime = AudioSettings.dspTime;
        key.hasBeenProcessed = false;
        
        // Debug logging removed for performance
        
        OnKeyPressed?.Invoke(degree, key.lastPressTime);
    }
    
    void HandleKeyRelease(int degree)
    {
        var key = pianoKeys[degree];
        key.state = PianoKeyState.Released;
        key.lastReleaseTime = AudioSettings.dspTime;
        
        // Debug logging removed for performance
        
        OnKeyReleased?.Invoke(degree, key.lastReleaseTime);
        
        // Process as a tap (press + release) only once
        if (!key.hasBeenProcessed)
        {
            key.hasBeenProcessed = true;
            ProcessKeyTap(degree, key.lastPressTime);
        }
    }
    
    void ProcessKeyTap(int degree, double pressTime)
    {
        // Debug logging removed for performance
        
        OnKeyTapped?.Invoke(degree, pressTime);
    }
    
    void UpdateKeyStates()
    {
        // This method can be used for additional state management if needed
        // For now, it's empty but can be extended for more complex behaviors
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
    
    // Public methods for external access
    public PianoKeyState GetKeyState(int degree)
    {
        return pianoKeys.ContainsKey(degree) ? pianoKeys[degree].state : PianoKeyState.Idle;
    }
    
    public bool IsKeyHeld(int degree)
    {
        return GetKeyState(degree) == PianoKeyState.Pressed;
    }
    
    public double GetKeyPressTime(int degree)
    {
        return pianoKeys.ContainsKey(degree) ? pianoKeys[degree].lastPressTime : 0;
    }
}

