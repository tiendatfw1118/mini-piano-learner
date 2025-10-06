using UnityEngine;
using System.Collections.Generic;
using SpeedItUp.Events;
using SpeedItUp.Input;
using SpeedItUp.Strategies;
using SpeedItUp.States;

/// <summary>
/// JudgeController – handles input, determines timing accuracy, and manages note state.
/// Design:
/// - Command pattern for input handling (via InputCommandManager/ImprovedInputManager).
/// - State machine for note lifecycle transitions.
/// - Observer pattern for publishing/consuming game events.
/// - Strategy pattern for note processing logic.
/// </summary>
public class JudgeController : MonoBehaviour
{
    [Header("Dependencies")]
    public Conductor conductor;
    public RemoteConfigData cfg;
    public TempoController tempo;
    public NoteSpawner spawner;
    public ScaleNoteAudio noteAudio;
    public AudioSource sfxHit, sfxMiss;
    public TMPro.TMP_Text feedbackText;
    public PerformanceMixController mix;

    [Header("Settings")]
    public bool requireCorrectDegree = true;
    public bool muteMissOnDegree = true;
    public bool ignoreTapsFarFromNotes = true;
    public float extraInputOffsetMs = 0f;
    public float visualOffsetMs = 0f; // Visual timing offset to match NoteMovement
    public bool autoHeadForHoldWhileHeld = true;

    [Header("Settings")]
    public bool forceAllowAllTaps = true;

    // Design pattern components
    private InputCommandManager commandManager;
    private NoteProcessor noteProcessor;
    private List<NoteMovement> activeHolds = new List<NoteMovement>();

    // Event subscriptions
    private List<System.IDisposable> eventSubscriptions = new List<System.IDisposable>();

    void Start()
    {
        InitializeComponents();
        SubscribeToEvents();
        ConnectToLegacyInput();
    }

    void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    private void InitializeComponents()
    {
        // Initialize command manager
        commandManager = FindFirstObjectByType<InputCommandManager>();
        if (commandManager == null)
        {
            var go = new GameObject("InputCommandManager");
            commandManager = go.AddComponent<InputCommandManager>();
        }

        // Initialize note processor with strategy pattern
        noteProcessor = new NoteProcessor(this, cfg);

        // Connect to improved input manager
        var inputManager = FindFirstObjectByType<ImprovedInputManager>();
        if (inputManager != null)
        {
            inputManager.OnInputEvent += HandleInputEvent;
        }

        if (mix == null) mix = FindFirstObjectByType<PerformanceMixController>(FindObjectsInactive.Exclude);
    }

    private void ConnectToLegacyInput()
    {
        // Connect to the old TapInput system for backward compatibility
        var tapInput = FindFirstObjectByType<TapInput>();
        if (tapInput != null)
        {
            // REMOVED: OnTapDegreeDsp - causes double processing with OnDegreeDownDsp
            tapInput.OnDegreeDownDsp += OnDegreeDown;
            tapInput.OnDegreeUpDsp += OnDegreeUp;
            
        }

        // Also connect to PianoInputState if it exists
        var pianoInputState = FindFirstObjectByType<PianoInputState>();
        if (pianoInputState != null)
        {
            // REMOVED: OnKeyTapped - causes double processing with OnKeyPressed
            pianoInputState.OnKeyPressed += OnDegreeDown;
            pianoInputState.OnKeyReleased += OnDegreeUp;
            
        }
    }

    private void SubscribeToEvents()
    {
        // Subscribe to game events using Observer pattern
        GameEventBus.SubscribeToNoteHit(OnNoteHit);
        GameEventBus.SubscribeToNoteMissed(OnNoteMissed);
        GameEventBus.SubscribeToHoldStarted(OnHoldStarted);
        GameEventBus.SubscribeToHoldCompleted(OnHoldCompleted);
        GameEventBus.SubscribeToHoldBroken(OnHoldBroken);
    }

    private void UnsubscribeFromEvents()
    {
        // Unsubscribe from all events
        GameEventBus.UnsubscribeFromNoteHit(OnNoteHit);
        GameEventBus.UnsubscribeFromNoteMissed(OnNoteMissed);
        GameEventBus.UnsubscribeFromHoldStarted(OnHoldStarted);
        GameEventBus.UnsubscribeFromHoldCompleted(OnHoldCompleted);
        GameEventBus.UnsubscribeFromHoldBroken(OnHoldBroken);

        // Unsubscribe from input manager
        var inputManager = FindFirstObjectByType<ImprovedInputManager>();
        if (inputManager != null)
        {
            inputManager.OnInputEvent -= HandleInputEvent;
        }

        // Unsubscribe from legacy input systems
        var tapInput = FindFirstObjectByType<TapInput>();
        if (tapInput != null)
        {
            // REMOVED: OnTapDegreeDsp - causes double processing with OnDegreeDownDsp
            tapInput.OnDegreeDownDsp -= OnDegreeDown;
            tapInput.OnDegreeUpDsp -= OnDegreeUp;
        }

        var pianoInputState = FindFirstObjectByType<PianoInputState>();
        if (pianoInputState != null)
        {
            // REMOVED: OnKeyTapped - causes double processing with OnKeyPressed
            pianoInputState.OnKeyPressed -= OnDegreeDown;
            pianoInputState.OnKeyReleased -= OnDegreeUp;
        }
    }

    void Update()
    {
        if (!spawner || spawner.LiveNotes == null || conductor == null || cfg == null) return;

        UpdateActiveHolds();
        ProcessAutoHoldDetection();
    }

    private void UpdateActiveHolds()
    {
        double bpm = conductor.bpm;
        // Use visual timing for completion (same as NoteMovement positioning)
        double nowB = conductor.SongBeats - (visualOffsetMs / 1000.0) * (bpm / 60.0);
        double goodB = MsToBeats(cfg.hitWindowMs.good, bpm);

        for (int i = activeHolds.Count - 1; i >= 0; i--)
        {
            var n = activeHolds[i];
            var d = n.GetComponent<NoteData>();
            if (d == null) 
            { 
                activeHolds.RemoveAt(i); 
                continue; 
            }

            double endB = d.targetBeat; // Use targetBeat (head) for completion timing
            bool stillHeld = IsKeyHeld(d.degree);
            double timeToEnd = endB - nowB;


            // Transition from HoldStarted to HoldActive if key is still held (simple, no loops)
            if (d.CurrentState == NoteState.HoldStarted && stillHeld)
            {
                d.ChangeState(NoteState.HoldActive);
            }

            // Check if hold should be broken (key released before head reaches hit line)
            if (!stillHeld && timeToEnd > MsToBeats(20, bpm))
            {
                BreakHold(d, "key released too early");
                activeHolds.RemoveAt(i);
                continue;
            }

            // Check if hold should be completed (when head has passed the hit line)
            if (timeToEnd <= 0) // Head has reached or passed the hit line
            {
                CompleteHold(d);
                activeHolds.RemoveAt(i);
            }
        }
    }

    private void ProcessAutoHoldDetection()
    {
        if (!autoHeadForHoldWhileHeld) return;

        double bpm = conductor.bpm;
        double nowB = conductor.SongBeats - MsToBeats(cfg.inputOffsetMs + extraInputOffsetMs, bpm);
        double goodB = MsToBeats(cfg.hitWindowMs.good, bpm);

        for (int i = spawner.LiveNotes.Count - 1; i >= 0; i--)
        {
            var n = spawner.LiveNotes[i];
            var d = n.GetComponent<NoteData>();
            if (d == null) continue;
            if (d.durationBeats <= 0f) continue; // Only hold notes
            if (d.headHit) continue; // Already started
            if (!IsKeyHeld(d.degree)) continue; // Only when key is held

            double ad = System.Math.Abs(d.targetBeat - nowB);
            if (ad <= goodB)
            {
                StartHold(d);
                if (!activeHolds.Contains(n)) activeHolds.Add(n);
            }
        }
    }

    private void HandleInputEvent(InputEvent inputEvent)
    {

        switch (inputEvent.Type)
        {
            case InputType.Tap:
                // REMOVED: ProcessTap - treat tap as hold start for hold-based gameplay
                ProcessHoldStart(inputEvent.Degree, inputEvent.Timestamp);
                break;
            case InputType.HoldStart:
                ProcessHoldStart(inputEvent.Degree, inputEvent.Timestamp);
                break;
            case InputType.HoldEnd:
                ProcessHoldEnd(inputEvent.Degree, inputEvent.Timestamp);
                break;
        }
    }

    // REMOVED: ProcessTap method - not needed since all notes are hold notes

    private void ProcessHoldStart(int degree, double timestamp)
    {

        // First, try to find a hold note to start
        var bestHoldNote = FindBestHoldNoteForDegree(degree);
        if (bestHoldNote != null)
        {
            var noteData = bestHoldNote.GetComponent<NoteData>();
            if (noteData.CanStartHold())
            {
                StartHold(noteData);
                if (!activeHolds.Contains(bestHoldNote)) activeHolds.Add(bestHoldNote);
                return; // Successfully started hold, don't process as regular note
            }
        }
        
        // If no hold note found or can't start hold, try as regular note
        var bestNote = FindBestNoteForDegree(degree);
        if (bestNote != null)
        {
            var noteData = bestNote.GetComponent<NoteData>();
            if (noteData.CanBeHit())
            {
                HitNote(noteData, timestamp);
                return; // Successfully hit note
            }
        }
        
        // If no note found, process as miss
        if (!ignoreTapsFarFromNotes || forceAllowAllTaps)
        {
            ProcessMiss(degree, timestamp);
        }
    }

    private void ProcessHoldEnd(int degree, double timestamp)
    {

        // Find active hold for this degree
        for (int i = activeHolds.Count - 1; i >= 0; i--)
        {
            var n = activeHolds[i];
            var d = n.GetComponent<NoteData>();
            if (d != null && d.degree == degree && d.isBeingHeld)
            {
                // Check if hold has been active long enough
                double holdDuration = AudioSettings.dspTime - d.headHitTime;
                if (holdDuration > 0.1) // 100ms minimum
                {
                    // Check if the hold should be completed instead of broken
                    double bpm = conductor.bpm;
                    // Use visual timing for completion (same as NoteMovement positioning)
                    double nowB = conductor.SongBeats - (visualOffsetMs / 1000.0) * (bpm / 60.0);
                    double endB = d.targetBeat; // Use targetBeat (head) for completion timing
                    double timeToEnd;
                    
                    // Use standard timing calculation (BPM compensation handled by BPMChangeHandler)
                    timeToEnd = endB - nowB;
                    double goodB = MsToBeats(cfg.hitWindowMs.good, bpm);
                    
                    // If the hold note head has reached the hit line, complete it instead of breaking
                    if (timeToEnd <= 0) // Head has reached or passed the hit line
                    {
                        CompleteHold(d);
                    }
                    else if (d.headHit)
                    {
                        // Hold was successfully started but released early - don't show feedback
                        d.ChangeState(NoteState.HoldBroken);
                        // Don't call BreakHold to avoid showing "Miss" feedback
                    }
                    else
                    {
                        // Hold was never successfully started - this is a real miss
                        BreakHold(d, "key released");
                    }
                activeHolds.RemoveAt(i);
                }
                break;
            }
        }
    }

    private NoteMovement FindBestNoteForDegree(int degree)
    {
        if (spawner?.LiveNotes == null) return null;

        double bpm = conductor.bpm;
        double nowB = conductor.SongBeats - MsToBeats(cfg.inputOffsetMs + extraInputOffsetMs, bpm);
        double goodB = MsToBeats(cfg.hitWindowMs.good, bpm);


        NoteMovement bestNote = null;
        double bestDistance = double.MaxValue;

        foreach (var n in spawner.LiveNotes)
        {
            var d = n.GetComponent<NoteData>();
            if (d == null || d.degree != degree) continue;
            // All notes are now hold notes, so we don't skip any
            if (d.CurrentState == NoteState.Hit) continue; // Skip already hit notes

            double distance;
            
            // Use standard distance calculation (BPM compensation handled by BPMChangeHandler)
            distance = System.Math.Abs(d.targetBeat - nowB);
            
            if (distance < goodB && distance < bestDistance)
            {
                bestDistance = distance;
                bestNote = n;
            }
        }

        return bestNote;
    }

    private NoteMovement FindBestHoldNoteForDegree(int degree)
    {
        if (spawner?.LiveNotes == null) return null;

        double bpm = conductor.bpm;
        double nowB = conductor.SongBeats - MsToBeats(cfg.inputOffsetMs + extraInputOffsetMs, bpm);
        double goodB = MsToBeats(cfg.hitWindowMs.good, bpm);

        NoteMovement bestNote = null;
        double bestDistance = double.MaxValue;

        foreach (var n in spawner.LiveNotes)
        {
            var d = n.GetComponent<NoteData>();
            if (d == null || d.degree != degree) continue;
            if (d.headHit) continue; // Only notes that haven't started holding
            if (d.CurrentState == NoteState.Hit) continue; // Skip already hit notes

            // For hold notes, check distance to tail (start of hold)
            double tailBeat = d.targetBeat - d.durationBeats;
            double tailDistance;
            
            // Use standard distance calculation (BPM compensation handled by BPMChangeHandler)
            tailDistance = System.Math.Abs(tailBeat - nowB);
            
            if (tailDistance < goodB && tailDistance < bestDistance)
            {
                bestDistance = tailDistance;
                bestNote = n;
            }
        }

        return bestNote;
    }

    private void HitNote(NoteData noteData, double timestamp)
    {
        // Hit note processing
        
        // Prevent double processing
        if (noteData.CurrentState == NoteState.Hit)
        {
            return;
        }
        
        double bpm = conductor.bpm;
        // Use the same distance calculation as FindBestNoteForDegree for consistency
        double nowB = conductor.SongBeats - MsToBeats(cfg.inputOffsetMs + extraInputOffsetMs, bpm);
        
        // All notes are now hold notes - calculate distance to tail (start of hold)
        double tailBeat = noteData.targetBeat - noteData.durationBeats;
        double distance = System.Math.Abs(tailBeat - nowB);

        Judge judge = noteProcessor.CalculateJudge(noteData, distance);
        
        // Update note state
        noteData.ChangeState(NoteState.Hit);
        // Note hit successfully
        
        // Play audio and visual feedback
        if (noteAudio) noteAudio.PlayDegree(noteData.degree);
        if (sfxHit) sfxHit.Play();
        
        // Update tempo and feedback (only show core timing feedback)
        tempo.OnJudge(judge);
        if (mix != null) mix.OnJudge(judge);
        string feedbackText = judge switch
        {
            Judge.Perfect => "Perfect",
            Judge.Great => "Great", 
            Judge.Good => "Good",
            Judge.Miss => "Miss", // For HitNote, just show "Miss" (no early/late context)
            _ => "Miss" // Default to Miss for any other case
        };
        SetFeedback(feedbackText);
        
        // Publish event
        GameEventBus.PublishNoteHit(noteData, judge);
        
        // Log note hit event
        double inputTime = conductor.SongBeats;
        double expectedTime = tailBeat;
        RhythmLogger.LogNote("note_hit", noteData.id, "current_song", expectedTime, inputTime);
        
        // Don't despawn note immediately - let it continue moving and get disabled by NoteSpawner
        // The note will turn grey when it passes the hit line and be removed when it passes the field
    }

    private void StartHold(NoteData noteData)
    {
        double bpm = conductor.bpm;
        double nowB = conductor.SongBeats - MsToBeats(cfg.inputOffsetMs + extraInputOffsetMs, bpm);
        double tailBeat = noteData.targetBeat - noteData.durationBeats;
        double tailDistance;
        
        // Use standard distance calculation (BPM compensation handled by BPMChangeHandler)
        tailDistance = System.Math.Abs(tailBeat - nowB);

        Judge judge = noteProcessor.CalculateJudge(noteData, tailDistance);
        
        // Update note state
        noteData.ChangeState(NoteState.HoldStarted);
        
        // Mark that the hold was successfully started (prevents double-counting as miss)
        noteData.headHit = true;

        // Play audio and visual feedback (only once, no loops)
        if (noteAudio) noteAudio.PlayDegree(noteData.degree);

        // Update tempo and feedback based on judge result
        tempo.OnJudge(judge);
        if (mix != null) mix.OnJudge(judge);
        
        // Set feedback based on judge result (only show core timing feedback)
        string feedbackText = judge switch
        {
            Judge.Perfect => "Perfect",
            Judge.Great => "Great", 
            Judge.Good => "Good",
            Judge.Miss => tailBeat > nowB ? "Early" : "Late",
            _ => "Miss" // Default to Miss for any other case
        };
        SetFeedback(feedbackText);
        
        // Publish event
        GameEventBus.PublishHoldStarted(noteData);
    }

    private void CompleteHold(NoteData noteData)
    {
        // Update note state
        noteData.ChangeState(NoteState.HoldCompleted);
        
        // Play audio and visual feedback (no sustain audio to stop)
        
        // Update tempo and feedback (no visual feedback for hold completion)
        tempo.OnJudge(Judge.Good);
        if (mix != null) mix.OnJudge(Judge.Good);
        // Don't show "Hold Complete" feedback - only show timing-based feedback
        
        // Publish event
        GameEventBus.PublishHoldCompleted(noteData);
        
        // Don't despawn note immediately - let it continue moving and get disabled by NoteSpawner
        // The note will turn grey when it passes the hit line and be removed when it passes the field
    }

    private void BreakHold(NoteData noteData, string reason)
    {
        
        // Update note state
        noteData.ChangeState(NoteState.HoldBroken);
        
        // Play audio and visual feedback (no sustain audio to stop)
        if (sfxMiss && !muteMissOnDegree) sfxMiss.Play();
        
        // DON'T count hold breaks as misses in streak system if the hold was already started
        // Only count as miss if this was a premature release (before the note was hit)
        if (!noteData.headHit)
        {
            // Only count as miss if the hold was never successfully started
            tempo.OnJudge(Judge.Miss);
            if (mix != null) mix.OnJudge(Judge.Miss);
            SetFeedback("Miss");
        }
        else
        {
            // Hold was already successfully started, don't show feedback (or show different feedback)
            // Don't show any feedback for successful holds that are released
        }
        
        // Publish event
        GameEventBus.PublishHoldBroken(noteData);
        
        // Don't despawn note immediately - let it continue moving and get disabled by NoteSpawner
        // The note will turn grey when it passes the hit line and be removed when it passes the field
    }

    public void ProcessMiss(int degree, double timestamp)
    {
        if (sfxMiss && !muteMissOnDegree) sfxMiss.Play();
        tempo.OnJudge(Judge.Miss);
        if (mix != null) mix.OnJudge(Judge.Miss);
        SetFeedback("Miss");
    }

    private bool IsKeyHeld(int degree)
    {
        // Prioritize TapInput for immediate key state tracking
        var tapInput = FindFirstObjectByType<TapInput>();
        if (tapInput != null)
        {
            return tapInput.IsHeld(degree);
        }

        // Try PianoInputState as second choice
        var pianoInputState = FindFirstObjectByType<PianoInputState>();
        if (pianoInputState != null)
        {
            return pianoInputState.IsKeyHeld(degree);
        }

        // Fallback to improved input manager
        var inputManager = FindFirstObjectByType<ImprovedInputManager>();
        if (inputManager != null)
        {
            return inputManager.IsKeyHeld(degree);
        }

        return false;
    }

    private void SetFeedback(string msg)
    {
        if (feedbackText) feedbackText.text = msg;
    }

    // Event handlers for Observer pattern
    private void OnNoteHit(NoteData noteData, Judge judge)
    {
    }

    private void OnNoteMissed(NoteData noteData)
    {    
        // Update tempo controller with miss
        tempo.OnJudge(Judge.Miss);
        
        // Show miss feedback
        SetFeedback("Miss");
    }

    private void OnHoldStarted(NoteData noteData)
    {
    }

    private void OnHoldCompleted(NoteData noteData)
    {
        // Event handler for hold completed
    }

    private void OnHoldBroken(NoteData noteData)
    {
        // Event handler for hold broken
    }

    // Public methods for compatibility with existing code
    // REMOVED: OnTapDegree and TryJudgeTap - not needed since all notes are hold notes
    public void OnDegreeDown(int degree, double _dsp) { ProcessHoldStart(degree, _dsp); }
    public void OnDegreeUp(int degree, double _dsp) { ProcessHoldEnd(degree, _dsp); }

    // Methods for strategy pattern
    // ProcessRegularNote removed - all notes are now hold notes

    public void ProcessHoldNoteStart(NoteData noteData, double tailDistance, double timestamp)
    {
        StartHold(noteData);
    }

    public void ProcessHoldNoteComplete(NoteData noteData, double timestamp)
    {
        CompleteHold(noteData);
    }

    public void ProcessHoldNoteRelease(NoteData noteData, double timestamp)
    {
        BreakHold(noteData, "manual release");
    }


    static double MsToBeats(double ms, double bpm) => (ms / 1000.0) * (bpm / 60.0);
    
    /// <summary>
    /// Reset the judge controller to initial state
    /// </summary>
    public void Reset()
    {
        // Clear active holds
        activeHolds.Clear();
        
        // Clear feedback text
        if (feedbackText)
        {
            feedbackText.text = "";
        }
    }
}


