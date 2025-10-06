using UnityEngine;
using SpeedItUp.States;

public class NoteData : MonoBehaviour
{
    public string id;
    public double targetBeat;
    public int lane;
    public int degree;
    public float durationBeats = 0f; // >0 => hold note
    public string noteName = "C"; // Note name for display (C, D, E, F, G, A, B)
    
    [System.NonSerialized] public bool headHit = false;
    [System.NonSerialized] public bool tailHit = false;
    [System.NonSerialized] public bool isBeingHeld = false;
    [System.NonSerialized] public double headHitTime = 0.0; // When the hold was started
    [System.NonSerialized] public bool hasPlayedGuideAudio = false; // Track if guide audio has been played
    
    // State machine integration
    private NoteStateMachine stateMachine;
    
    // Helper properties
    public bool IsHoldNote => durationBeats > 0f;
    public double TailBeat => targetBeat - durationBeats;
    
    // State machine access
    public NoteStateMachine StateMachine => stateMachine;
    public NoteState CurrentState => stateMachine?.CurrentState ?? NoteState.Spawned;
    
    void Start()
    {
        // Initialize state machine if not already initialized
        InitializeStateMachine();
    }
    
    /// <summary>
    /// Initialize the state machine (public method to avoid execution order issues)
    /// </summary>
    public void InitializeStateMachine()
    {
        if (stateMachine == null)
        {
            stateMachine = new NoteStateMachine(this);
            
            // Subscribe to state changes for debugging/logging
            stateMachine.OnStateChanged += OnStateChanged;
            stateMachine.OnStateEntered += OnStateEntered;
            stateMachine.OnStateExited += OnStateExited;
        }
    }
    
    void OnDestroy()
    {
        // Unsubscribe from events
        if (stateMachine != null)
        {
            stateMachine.OnStateChanged -= OnStateChanged;
            stateMachine.OnStateEntered -= OnStateEntered;
            stateMachine.OnStateExited -= OnStateExited;
        }
    }
    
    /// <summary>
    /// Change the note's state
    /// </summary>
    public bool ChangeState(NoteState newState)
    {
        if (stateMachine == null)
        {
            return false;
        }
        
        return stateMachine.ChangeState(newState);
    }
    
    /// <summary>
    /// Check if note can be hit
    /// </summary>
    public bool CanBeHit()
    {
        return stateMachine?.CanBeHit() ?? false;
    }
    
    /// <summary>
    /// Check if hold can be started
    /// </summary>
    public bool CanStartHold()
    {
        return stateMachine?.CanStartHold() ?? false;
    }
    
    /// <summary>
    /// Check if hold can be released
    /// </summary>
    public bool CanReleaseHold()
    {
        return stateMachine?.CanReleaseHold() ?? false;
    }
    
    /// <summary>
    /// Check if note is in a terminal state
    /// </summary>
    public bool IsTerminalState()
    {
        return stateMachine?.IsTerminalState() ?? false;
    }
    
    // Event handlers for state changes
    private void OnStateChanged(NoteState oldState, NoteState newState)
    {
        // State changed - can be used for debugging if needed
    }
    
    private void OnStateEntered(NoteData noteData, NoteState state)
    {
        // Handle state entry logic
        switch (state)
        {
            case NoteState.Active:
                // Note is now active and can be hit
                break;
            case NoteState.Hit:
                headHit = true;
                break;
            case NoteState.HoldStarted:
                headHit = true;
                headHitTime = AudioSettings.dspTime;
                isBeingHeld = true;
                break;
            case NoteState.HoldActive:
                isBeingHeld = true;
                break;
            case NoteState.HoldCompleted:
                tailHit = true;
                isBeingHeld = false;
                break;
            case NoteState.HoldBroken:
                isBeingHeld = false;
                break;
        }
    }
    
    private void OnStateExited(NoteData noteData, NoteState state)
    {
        // Handle state exit logic if needed
    }
}
