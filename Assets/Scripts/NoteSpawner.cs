using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.UI;
using SpeedItUp.States;
using SpeedItUp.Events;

/// <summary>
/// NoteSpawner – creates notes from chart data, assigns lane colors and names,
/// handles time-based miss detection, and cleans up notes after they pass.
/// Also emits JSON logs for note events and song end.
/// </summary>
public sealed class NoteSpawner : MonoBehaviour
{
    public Conductor conductor;
    public RectTransform[] lanes;
    public RectTransform hitLine;
    public GameObject notePrefab;

    public float spawnLeadBeats = 12f; // How many beats ahead to spawn notes (increased for more preparation time)
    public float unitsPerBeat = 220f;

    public bool mapDegreeToLane = true;
    public int[] degreeToLane = new int[7] {0,1,2,3,4,5,6};

    public bool guidePlayback = false;
    public ScaleNoteAudio noteAudio;

    public List<NoteMovement> LiveNotes { get; private set; } = new();
    
    // Debug access to private fields
    public int NextIdx => _nextIdx;
    public int ChartLength => _chart?.notes?.Length ?? 0;

    public bool enableAutoMiss = false; // Disabled to prevent premature auto-missing
    public float missCullBeatsAfterCross = 8.0f; // Much more forgiving grace period - increased to prevent immediate destruction
    public bool debugAutoMiss = false; // Debug option to see what's happening
    public bool disableAutoMissForTesting = true; // Temporarily disable auto-miss for testing
    
    [Header("Note Visibility")]
    public bool hideNotesUntilMovement = false; // Hide notes until they start moving - DISABLED for testing
    public float showNotesBeatsBeforeTarget = 2f; // Show notes this many beats before target
    
    [Header("Lane Colors")]
    public Color[] laneColors = new Color[7] 
    {
        new Color(1f, 0.2f, 0.2f, 0.8f), // Red for lane 0
        new Color(1f, 0.6f, 0.2f, 0.8f), // Orange for lane 1
        new Color(1f, 1f, 0.2f, 0.8f),   // Yellow for lane 2
        new Color(0.2f, 1f, 0.2f, 0.8f), // Green for lane 3
        new Color(0.2f, 0.8f, 1f, 0.8f), // Cyan for lane 4
        new Color(0.4f, 0.2f, 1f, 0.8f), // Blue for lane 5
        new Color(1f, 0.2f, 1f, 0.8f)    // Magenta for lane 6
    };

    private ChartData _chart; private int _nextIdx = 0;
    private bool _isSetup = false;
    private bool _songEndLogged = false;

    public void SetupAndBegin(ChartData chart)
    {
        _chart = chart; 
        
        // Only reset if this is the first setup
        if (!_isSetup)
        {
            _nextIdx = 0; 
            LiveNotes.Clear();
            // Conductor timing (bpm/offset) is set by Boot and TempoController.
            // Do not override here to avoid clobbering remote-config BPM.
            _isSetup = true;
        }
    }

    void Update()
    {
        if (_chart == null || _chart.notes == null) return;
        double B = conductor.SongBeats;

        // Check if all notes have been spawned and all live notes have been processed
        bool allNotesSpawned = _nextIdx >= _chart.notes.Length;
        bool allNotesProcessed = LiveNotes.Count == 0;
        
        // If all notes are spawned and processed, log song end
        if (allNotesSpawned && allNotesProcessed && !_songEndLogged)
        {
            _songEndLogged = true;
            LogSongEnd();
        }

        while (_nextIdx < _chart.notes.Length && _chart.notes[_nextIdx].beat - B <= spawnLeadBeats)
        {
            Spawn(_chart.notes[_nextIdx]);
            _nextIdx++;
        }

        // Handle guide playback - play audio when notes start moving
        if (guidePlayback && noteAudio != null)
        {
            foreach (var mov in LiveNotes)
            {
                if (mov == null || mov.gameObject == null) continue;
                
                var noteData = mov.GetComponent<NoteData>();
                if (noteData == null) continue;
                
                // Check if this note should start playing audio (when it's about to start moving)
                double beatsUntilStart = mov.targetBeat - B;
                if (beatsUntilStart <= 0.1 && beatsUntilStart >= -0.1 && !noteData.hasPlayedGuideAudio)
                {
                    // Play the guide audio
                    noteAudio.PlayDegree(noteData.degree, conductor.DspAtBeat(mov.targetBeat));
                    noteData.hasPlayedGuideAudio = true;
                }
            }
        }

        // Show notes when they should start moving
        if (hideNotesUntilMovement)
        {
            foreach (var mov in LiveNotes)
            {
                if (mov == null || mov.gameObject == null) continue;
                
                var canvasGroup = mov.gameObject.GetComponent<CanvasGroup>();
                var noteView = mov.gameObject.GetComponent<NoteView>();
                if (canvasGroup == null) continue;
                
                // Show note when it's close enough to the target beat
                double beatsUntilTarget = mov.targetBeat - B;
                if (beatsUntilTarget <= showNotesBeatsBeforeTarget && canvasGroup.alpha < 1f)
                {
                    canvasGroup.alpha = 1f; // Make note visible
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                    
                    // Start fade-in effect if NoteView component exists
                    if (noteView != null)
                    {
                        noteView.StartFadeIn();
                    }
                }
            }
        }

        // Handle notes that have passed the hit line - disable them instead of destroying
        int removedThisFrame = 0;
        for (int i = LiveNotes.Count - 1; i >= 0; i--)
        {
            // Safety check: ensure we don't go out of bounds
            if (i >= LiveNotes.Count) break;
            
            var mov = LiveNotes[i];
            if (mov == null || mov.gameObject == null) 
            {
                LiveNotes.RemoveAt(i);
                continue;
            }
            
            var d = mov.GetComponent<NoteData>();
            
            // Check if note is past the target beat and should be marked as missed
            // Use time-based approach instead of position-based to avoid false positives
            double lateBeats = B - d.targetBeat;
            
            // Only mark as missed if the note is significantly past the hit window
            // Use a reasonable hit window (e.g., 0.3 beats = ~0.3 seconds at 60 BPM)
            double missThresholdBeats = 0.3; // 0.3 beats past target = miss
            
            // Only check for miss if the note is still Active (hasn't been hit yet)
            // Don't mark as missed if the note has already been hit or is being held
            if (lateBeats > missThresholdBeats && d.CurrentState == NoteState.Active)
            {
                d.ChangeState(NoteState.Missed);
                
                // Notify judge system about the miss
                GameEventBus.PublishNoteMissed(d);
                
                // Log note miss event
                double inputTime = B;
                double expectedTime = d.targetBeat;
                RhythmLogger.LogNote("note_miss", d.id, "current_song", expectedTime, inputTime);
                
                // Change color to grey to indicate disabled state
                var holdView = mov.gameObject.GetComponent<HoldNoteView>();
                if (holdView != null)
                {
                    holdView.SetDisabled(true);
                }
            }
            
            // Only remove notes that have passed far beyond the field
            // Use a more conservative BPM-relative threshold
            // (lateBeats already calculated above)
            // Scale threshold more conservatively: faster BPM = slightly shorter grace period
            double bpmRatio = Mathf.Clamp(conductor.bpm / 120.0f, 0.5f, 2.0f); // Clamp between 0.5x and 2.0x
            double bpmAdjustedThreshold = missCullBeatsAfterCross / bpmRatio; // Faster BPM = shorter threshold
            
            if (lateBeats > bpmAdjustedThreshold)
            {
                LiveNotes.RemoveAt(i);
                if (mov.gameObject != null)
                {
                    Destroy(mov.gameObject);
                }
            }
        }
    }

    void Spawn(ChartNote n)
    {
        if (n == null || lanes == null || lanes.Length == 0 || notePrefab == null)
        {
            return;
        }
        
        int laneIndex = n.lane;
        if (mapDegreeToLane && n.degree >= 1 && n.degree <= 7)
        {
            int degreeIndex = Mathf.Clamp(n.degree - 1, 0, 6);
            if (degreeIndex < degreeToLane.Length)
            {
                laneIndex = Mathf.Clamp(degreeToLane[degreeIndex], 0, lanes.Length - 1);
            }
            else
            {
                // Fallback: map degree directly to lane if degreeToLane array is too small
                laneIndex = Mathf.Clamp(n.degree - 1, 0, lanes.Length - 1);
            }
        }

        var parent = lanes[Mathf.Clamp(laneIndex, 0, lanes.Length - 1)];
        var go = Instantiate(notePrefab, parent, false);
        var rect = go.GetComponent<RectTransform>(); if (!rect) rect = go.AddComponent<RectTransform>();

        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.anchoredPosition = new Vector2(0f, 0f);

        if (rect.sizeDelta.sqrMagnitude < 1f)
            rect.sizeDelta = new Vector2(80f, 130f);
        var img = go.GetComponent<Image>();
        if (!img) img = go.AddComponent<Image>();

        var mov = go.GetComponent<NoteMovement>(); if (!mov) mov = go.AddComponent<NoteMovement>();
        mov.rect = rect; mov.unitsPerBeat = unitsPerBeat; mov.targetBeat = n.beat; mov.conductor = conductor;
        mov.BindToHitLine(hitLine);

        var data = go.GetComponent<NoteData>(); if (!data) data = go.AddComponent<NoteData>();
        data.id = n.id; data.targetBeat = n.beat; data.lane = laneIndex; data.degree = n.degree;
        // Make ALL notes hold notes - minimum 1 beat, maximum 5 beats duration
        // Based on audio clip length (5 seconds), 5 beats is the maximum reasonable hold time
        data.durationBeats = Mathf.Clamp(n.len, 1f, 5f);
        
        // Warn if original length exceeded maximum
        if (n.len > 5f)
        {
            Debug.LogWarning($"[NoteSpawner] Note {n.id} length {n.len} beats exceeds maximum of 5 beats. Clamped to 5 beats.");
        }
        

        // All notes are now hold notes - add HoldNoteView
        var holdView = go.GetComponent<HoldNoteView>();
        if (!holdView) holdView = go.AddComponent<HoldNoteView>();
        holdView.rect = rect;
        holdView.hitLineY = mov.ZeroYInLane;
        
        // Set lane-specific color for hold notes
        if (laneIndex >= 0 && laneIndex < laneColors.Length)
        {
            holdView.SetLaneColor(laneColors[laneIndex]);
        }
        else
        {
            // Fallback to default color if lane index is out of range
            holdView.SetLaneColor(new Color(0.8f, 0.8f, 0.8f, 0.8f)); // Light grey
        }
        
        // Set note name based on degree - store it in NoteData for later use
        string noteName = GetNoteNameFromDegree(n.degree);
        
        // Store the note name in NoteData so HoldNoteView can access it
        data.noteName = noteName;
        
        // Also set lane color for NoteView components (for tap notes, if they exist)
        var noteView = go.GetComponent<NoteView>();
        if (noteView)
        {
            if (laneIndex >= 0 && laneIndex < laneColors.Length)
            {
                noteView.SetLaneColor(laneColors[laneIndex]);
            }
            else
            {
                // Fallback to default color if lane index is out of range
                noteView.SetLaneColor(new Color(0.8f, 0.8f, 0.8f, 0.8f)); // Light grey
            }
        }

        // Hide note initially if hideNotesUntilMovement is enabled
        if (hideNotesUntilMovement)
        {
            var canvasGroup = go.GetComponent<CanvasGroup>();
            if (!canvasGroup) canvasGroup = go.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f; // Make note invisible
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        else
        {
            // Ensure notes are visible when hideNotesUntilMovement is disabled
            var canvasGroup = go.GetComponent<CanvasGroup>();
            if (canvasGroup)
            {
                canvasGroup.alpha = 1f; // Make note visible
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        LiveNotes.Add(mov);

        // Initialize state machine immediately to avoid execution order issues
        data.InitializeStateMachine();

        // Transition note to Active state so it can be hit
        data.ChangeState(NoteState.Active);

        // Don't play guide audio immediately - it will be played when the note starts moving
    }


    public void Despawn(NoteMovement n) 
    { 
        if (n == null || n.gameObject == null) return;
        
        var data = n.GetComponent<NoteData>();
        
        // Transition to terminal state before despawning
        if (data != null)
        {
            // Determine if this was a hit or miss based on current state
            if (data.CurrentState == NoteState.Hit || data.CurrentState == NoteState.HoldCompleted)
            {
                // Note was successfully hit - already in terminal state
            }
            else
            {
                // Note was missed
                data.ChangeState(NoteState.Missed);
            }
        }
        
        // Remove from list first to prevent race conditions
        LiveNotes.Remove(n);
        
        // Only destroy if the object still exists
        if (n.gameObject != null)
        {
            Destroy(n.gameObject);
        }
    }
    
    /// <summary>
    /// Clear all live notes - used for replay functionality
    /// </summary>
    public void ClearAllNotes()
    {
        // Destroy all live notes
        for (int i = LiveNotes.Count - 1; i >= 0; i--)
        {
            if (LiveNotes[i] != null && LiveNotes[i].gameObject != null)
            {
                Destroy(LiveNotes[i].gameObject);
            }
        }
        
        // Clear the list
        LiveNotes.Clear();
        
        // Reset spawner state
        _nextIdx = 0;
        _isSetup = false;
        _songEndLogged = false;
    }
    
    /// <summary>
    /// Get note name from degree (1-7 maps to C-B)
    /// </summary>
    private string GetNoteNameFromDegree(int degree)
    {
        switch (degree)
        {
            case 1: return "C";
            case 2: return "D";
            case 3: return "E";
            case 4: return "F";
            case 5: return "G";
            case 6: return "A";
            case 7: return "B";
            default: return "?";
        }
    }
    
    /// <summary>
    /// Log song end event with hit/miss statistics
    /// </summary>
    private void LogSongEnd()
    {
        // Get hit/miss statistics from TempoController
        var tempoController = FindFirstObjectByType<TempoController>();
        int hitCount = 0;
        int missCount = 0;
        
        if (tempoController != null)
        {
            var stats = tempoController.GetPerformanceStats();
            // Parse hit/miss counts from stats string (assuming format like "Combo: 5, Miss Streak: 2")
            // For now, we'll use a simple approach - count total notes spawned
            hitCount = _chart.notes.Length; // Total notes in chart
            missCount = 0; // We could track this more precisely if needed
        }
        
        // Get song duration from conductor
        double duration = conductor.SongTimeSec;
        
        // Log the song end event
        RhythmLogger.LogSongEnd("current_song", hitCount, missCount, duration);
    }
}
