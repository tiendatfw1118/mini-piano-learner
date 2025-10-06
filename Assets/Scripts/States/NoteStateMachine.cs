using UnityEngine;

namespace SpeedItUp.States
{
    /// <summary>
    /// States that a note can be in
    /// </summary>
    public enum NoteState
    {
        Spawned,        // Note has been spawned but not yet active
        Active,         // Note is moving down the lane
        Hit,            // Note has been successfully hit
        Missed,         // Note was missed
        HoldStarted,    // Hold note has been started
        HoldActive,     // Hold note is being held
        HoldCompleted,  // Hold note was completed successfully
        HoldBroken      // Hold note was broken (released too early)
    }

    /// <summary>
    /// State machine for managing note states
    /// </summary>
    public class NoteStateMachine
    {
        private NoteState currentState;
        private NoteData noteData;
        private double stateStartTime;
        private double lastStateChangeTime;

        // Events for state changes
        public System.Action<NoteState, NoteState> OnStateChanged;
        public System.Action<NoteData, NoteState> OnStateEntered;
        public System.Action<NoteData, NoteState> OnStateExited;

        public NoteStateMachine(NoteData noteData)
        {
            this.noteData = noteData;
            this.currentState = NoteState.Spawned;
            this.stateStartTime = AudioSettings.dspTime;
            this.lastStateChangeTime = AudioSettings.dspTime;
        }

        /// <summary>
        /// Get current state
        /// </summary>
        public NoteState CurrentState => currentState;

        /// <summary>
        /// Get time spent in current state
        /// </summary>
        public double TimeInCurrentState => AudioSettings.dspTime - stateStartTime;

        /// <summary>
        /// Get time since last state change
        /// </summary>
        public double TimeSinceLastChange => AudioSettings.dspTime - lastStateChangeTime;

        /// <summary>
        /// Change to a new state
        /// </summary>
        public bool ChangeState(NoteState newState)
        {
            if (!CanTransitionTo(newState))
            {
                return false;
            }

            var oldState = currentState;
            
            // Exit current state
            OnStateExited?.Invoke(noteData, oldState);
            
            // Change state
            currentState = newState;
            stateStartTime = AudioSettings.dspTime;
            lastStateChangeTime = AudioSettings.dspTime;
            
            // Enter new state
            OnStateEntered?.Invoke(noteData, newState);
            OnStateChanged?.Invoke(oldState, newState);
            
            return true;
        }

        /// <summary>
        /// Check if transition to new state is valid
        /// </summary>
        private bool CanTransitionTo(NoteState newState)
        {
            switch (currentState)
            {
                case NoteState.Spawned:
                    return newState == NoteState.Active;
                    
                case NoteState.Active:
                    return newState == NoteState.Hit || 
                           newState == NoteState.Missed || 
                           newState == NoteState.HoldStarted;
                    
                case NoteState.HoldStarted:
                    return newState == NoteState.HoldActive || 
                           newState == NoteState.HoldBroken;
                    
                case NoteState.HoldActive:
                    return newState == NoteState.HoldCompleted || 
                           newState == NoteState.HoldBroken;
                    
                case NoteState.Hit:
                case NoteState.Missed:
                case NoteState.HoldCompleted:
                case NoteState.HoldBroken:
                    return false; // Terminal states
                    
                default:
                    return false;
            }
        }

        /// <summary>
        /// Check if current state is terminal (note is finished)
        /// </summary>
        public bool IsTerminalState()
        {
            return currentState == NoteState.Hit ||
                   currentState == NoteState.Missed ||
                   currentState == NoteState.HoldCompleted ||
                   currentState == NoteState.HoldBroken;
                   // Timing states are not terminal - they can transition to HoldActive
        }

        /// <summary>
        /// Check if note can be hit
        /// </summary>
        public bool CanBeHit()
        {
            return currentState == NoteState.Active;
        }

        /// <summary>
        /// Check if hold can be started
        /// </summary>
        public bool CanStartHold()
        {
            return currentState == NoteState.Active && noteData.IsHoldNote;
        }

        /// <summary>
        /// Check if hold can be released
        /// </summary>
        public bool CanReleaseHold()
        {
            return currentState == NoteState.HoldActive;
        }
    }
}

