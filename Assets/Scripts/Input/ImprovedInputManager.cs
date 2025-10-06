using UnityEngine;
using UnityEngine.InputSystem;
using SpeedItUp.Events;
using SpeedItUp.Input;
using System.Collections.Generic;

namespace SpeedItUp.Input
{
    /// <summary>
    /// Improved input manager using design patterns
    /// Handles input detection, state management, and command generation
    /// </summary>
    public class ImprovedInputManager : MonoBehaviour
    {
        [Header("Input Settings")]
        public float holdThreshold = 0.1f; // Minimum time to consider a hold
        public float tapThreshold = 0.2f;  // Maximum time for a tap
        
        [Header("Debug Options")]
        public bool debugInput = false;
        
        private InputCommandManager commandManager;
        private Dictionary<int, KeyState> keyStates = new Dictionary<int, KeyState>();
        private Dictionary<int, double> keyPressTimes = new Dictionary<int, double>();
        
        // Events
        public System.Action<InputEvent> OnInputEvent;

        void Start()
        {
            InitializeKeyStates();
            commandManager = FindFirstObjectByType<InputCommandManager>();
            if (commandManager == null)
            {
                var go = new GameObject("InputCommandManager");
                commandManager = go.AddComponent<InputCommandManager>();
            }
        }

        void Update()
        {
            HandleKeyboardInput();
            UpdateKeyStates();
        }

        private void InitializeKeyStates()
        {
            for (int i = 1; i <= 7; i++)
            {
                keyStates[i] = new KeyState();
            }
        }

        private void HandleKeyboardInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            for (int degree = 1; degree <= 7; degree++)
            {
                bool isPressed = IsKeyPressed(degree, kb);
                var keyState = keyStates[degree];

                switch (keyState.State)
                {
                    case KeyStateType.Idle:
                        if (isPressed)
                        {
                            HandleKeyPress(degree);
                        }
                        break;

                    case KeyStateType.Pressed:
                        if (!isPressed)
                        {
                            HandleKeyRelease(degree);
                        }
                        else
                        {
                            // Check if this should become a hold
                            double pressDuration = AudioSettings.dspTime - keyPressTimes[degree];
                            if (pressDuration >= holdThreshold && keyState.State == KeyStateType.Pressed)
                            {
                                HandleHoldStart(degree);
                            }
                        }
                        break;

                    case KeyStateType.Holding:
                        if (!isPressed)
                        {
                            HandleHoldEnd(degree);
                        }
                        break;
                }
            }
        }

        private bool IsKeyPressed(int degree, Keyboard kb)
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

        private void HandleKeyPress(int degree)
        {
            var keyState = keyStates[degree];
            keyState.State = KeyStateType.Pressed;
            keyPressTimes[degree] = AudioSettings.dspTime;

            // Debug logging removed for performance

            // Create input event
            var inputEvent = new InputEvent(InputType.Press, degree, keyPressTimes[degree]);
            OnInputEvent?.Invoke(inputEvent);
            GameEventBus.PublishInputEvent(inputEvent);
        }

        private void HandleKeyRelease(int degree)
        {
            var keyState = keyStates[degree];
            double pressDuration = AudioSettings.dspTime - keyPressTimes[degree];
            double releaseTime = AudioSettings.dspTime;

            // Debug logging removed for performance

            // Determine if this was a tap or hold release
            if (keyState.State == KeyStateType.Pressed && pressDuration < tapThreshold)
            {
                // This was a tap
                HandleTap(degree, pressDuration);
            }
            else if (keyState.State == KeyStateType.Holding)
            {
                // This was a hold release
                HandleHoldEnd(degree);
            }

            keyState.State = KeyStateType.Idle;
        }

        private void HandleTap(int degree, double duration)
        {
            // Debug logging removed for performance

            // Create tap command
            var judgeController = FindFirstObjectByType<JudgeController>();
            if (judgeController != null)
            {
                var tapCommand = new TapCommand(degree, AudioSettings.dspTime, judgeController);
                commandManager.QueueCommand(tapCommand);
            }

            // Create input event
            var inputEvent = new InputEvent(InputType.Tap, degree, AudioSettings.dspTime, duration);
            OnInputEvent?.Invoke(inputEvent);
            GameEventBus.PublishInputEvent(inputEvent);
        }

        private void HandleHoldStart(int degree)
        {
            var keyState = keyStates[degree];
            keyState.State = KeyStateType.Holding;

            // Debug logging removed for performance

            // Create hold start command
            var judgeController = FindFirstObjectByType<JudgeController>();
            if (judgeController != null)
            {
                var holdStartCommand = new HoldStartCommand(degree, AudioSettings.dspTime, judgeController);
                commandManager.QueueCommand(holdStartCommand);
            }

            // Create input event
            var inputEvent = new InputEvent(InputType.HoldStart, degree, AudioSettings.dspTime);
            OnInputEvent?.Invoke(inputEvent);
            GameEventBus.PublishInputEvent(inputEvent);
        }

        private void HandleHoldEnd(int degree)
        {
            var keyState = keyStates[degree];
            keyState.State = KeyStateType.Idle;

            // Debug logging removed for performance

            // Create hold end command
            var judgeController = FindFirstObjectByType<JudgeController>();
            if (judgeController != null)
            {
                var holdEndCommand = new HoldEndCommand(degree, AudioSettings.dspTime, judgeController);
                commandManager.QueueCommand(holdEndCommand);
            }

            // Create input event
            var inputEvent = new InputEvent(InputType.HoldEnd, degree, AudioSettings.dspTime);
            OnInputEvent?.Invoke(inputEvent);
            GameEventBus.PublishInputEvent(inputEvent);
        }

        private void UpdateKeyStates()
        {
            // Update any time-based state changes
            foreach (var kvp in keyStates)
            {
                var keyState = kvp.Value;
                // Add any time-based state updates here if needed
            }
        }

        public bool IsKeyHeld(int degree)
        {
            // Consider a key "held" if it's either Pressed or Holding
            // This is important for hold notes that need immediate response
            return keyStates.ContainsKey(degree) && 
                   (keyStates[degree].State == KeyStateType.Pressed || 
                    keyStates[degree].State == KeyStateType.Holding);
        }

        public KeyStateType GetKeyState(int degree)
        {
            return keyStates.ContainsKey(degree) ? keyStates[degree].State : KeyStateType.Idle;
        }
    }

    #region Supporting Classes

    public class KeyState
    {
        public KeyStateType State { get; set; } = KeyStateType.Idle;
        public double LastStateChangeTime { get; set; }
    }

    public enum KeyStateType
    {
        Idle,
        Pressed,
        Holding
    }

    public class TapCommand : IInputCommand
    {
        private readonly int degree;
        private readonly double timestamp;
        private readonly JudgeController judgeController;

        public TapCommand(int degree, double timestamp, JudgeController judgeController)
        {
            this.degree = degree;
            this.timestamp = timestamp;
            this.judgeController = judgeController;
        }

        public void Execute()
        {
            // REMOVED: TryJudgeTap - not needed since all notes are hold notes
            // Use ProcessHoldStart instead for hold-based gameplay
            judgeController.OnDegreeDown(degree, timestamp);
        }

        public bool CanExecute() => true;
        public double GetTimestamp() => timestamp;
        public int GetDegree() => degree;
    }

    public class HoldStartCommand : IInputCommand
    {
        private readonly int degree;
        private readonly double timestamp;
        private readonly JudgeController judgeController;

        public HoldStartCommand(int degree, double timestamp, JudgeController judgeController)
        {
            this.degree = degree;
            this.timestamp = timestamp;
            this.judgeController = judgeController;
        }

        public void Execute()
        {
            judgeController.OnDegreeDown(degree, timestamp);
        }

        public bool CanExecute() => true;
        public double GetTimestamp() => timestamp;
        public int GetDegree() => degree;
    }

    public class HoldEndCommand : IInputCommand
    {
        private readonly int degree;
        private readonly double timestamp;
        private readonly JudgeController judgeController;

        public HoldEndCommand(int degree, double timestamp, JudgeController judgeController)
        {
            this.degree = degree;
            this.timestamp = timestamp;
            this.judgeController = judgeController;
        }

        public void Execute()
        {
            judgeController.OnDegreeUp(degree, timestamp);
        }

        public bool CanExecute() => true;
        public double GetTimestamp() => timestamp;
        public int GetDegree() => degree;
    }

    #endregion
}
