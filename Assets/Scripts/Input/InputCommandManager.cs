using System.Collections.Generic;
using UnityEngine;

namespace SpeedItUp.Input
{
    /// <summary>
    /// Manages input commands using Command Pattern
    /// Prevents race conditions and ensures proper execution order
    /// </summary>
    public class InputCommandManager : MonoBehaviour
    {
        [Header("Debug Options")]
        public bool debugCommands = false;
        
        private Queue<IInputCommand> commandQueue = new Queue<IInputCommand>();
        private List<IInputCommand> commandHistory = new List<IInputCommand>();
        private int maxHistorySize = 100;
        
        // Events for command processing
        public System.Action<IInputCommand> OnCommandExecuted;
        public System.Action<IInputCommand> OnCommandQueued;

        void Update()
        {
            ProcessCommandQueue();
        }

        /// <summary>
        /// Queue a command for execution
        /// </summary>
        public void QueueCommand(IInputCommand command)
        {
            if (command == null) return;

            // Debug logging removed for performance

            commandQueue.Enqueue(command);
            OnCommandQueued?.Invoke(command);
        }

        /// <summary>
        /// Process all queued commands in order
        /// </summary>
        private void ProcessCommandQueue()
        {
            while (commandQueue.Count > 0)
            {
                var command = commandQueue.Dequeue();
                
                if (command.CanExecute())
                {
                    // Debug logging removed for performance
                    
                    command.Execute();
                    AddToHistory(command);
                    OnCommandExecuted?.Invoke(command);
                }
                else if (debugCommands)
                {
                    // Command cannot execute - removed debug logging for performance
                }
            }
        }

        /// <summary>
        /// Add command to history for debugging/replay
        /// </summary>
        private void AddToHistory(IInputCommand command)
        {
            commandHistory.Add(command);
            
            // Keep history size manageable
            if (commandHistory.Count > maxHistorySize)
            {
                commandHistory.RemoveAt(0);
            }
        }

        /// <summary>
        /// Get command history for debugging
        /// </summary>
        public List<IInputCommand> GetCommandHistory()
        {
            return new List<IInputCommand>(commandHistory);
        }

        /// <summary>
        /// Clear all queued commands
        /// </summary>
        public void ClearQueue()
        {
            commandQueue.Clear();
        }

        /// <summary>
        /// Get current queue size
        /// </summary>
        public int GetQueueSize()
        {
            return commandQueue.Count;
        }
    }
}

