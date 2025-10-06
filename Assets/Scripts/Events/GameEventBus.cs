using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpeedItUp.Events
{
    /// <summary>
    /// Centralized event bus for game events
    /// Reduces coupling and makes event handling more predictable
    /// </summary>
    public class GameEventBus : MonoBehaviour
    {
        private static GameEventBus instance;
        public static GameEventBus Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("GameEventBus");
                    instance = go.AddComponent<GameEventBus>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        // Event definitions - initialize with empty delegates to prevent null issues
        public static event Action<NoteData, Judge> OnNoteHit = delegate { };
        public static event Action<NoteData> OnNoteMissed = delegate { };
        public static event Action<NoteData> OnHoldStarted = delegate { };
        public static event Action<NoteData> OnHoldCompleted = delegate { };
        public static event Action<NoteData> OnHoldBroken = delegate { };
        public static event Action<int> OnComboChanged = delegate { };
        public static event Action<float> OnBpmChanged = delegate { };
        public static event Action<InputEvent> OnInputEvent = delegate { };

        // Event queue for processing events in order
        private Queue<GameEvent> eventQueue = new Queue<GameEvent>();
        private List<GameEvent> eventHistory = new List<GameEvent>();
        private int maxHistorySize = 200;

        [Header("Debug Options")]
        public bool debugEvents = false;

        void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        void Update()
        {
            ProcessEventQueue();
        }

        #region Event Publishing

        public static void PublishNoteHit(NoteData noteData, Judge judge)
        {
            // Publishing NoteHit event
            Instance.QueueEvent(new NoteHitEvent(noteData, judge));
        }

        public static void PublishNoteMissed(NoteData noteData)
        {
            Instance.QueueEvent(new NoteMissedEvent(noteData));
        }

        public static void PublishHoldStarted(NoteData noteData)
        {
            Instance.QueueEvent(new HoldStartedEvent(noteData));
        }

        public static void PublishHoldCompleted(NoteData noteData)
        {
            Instance.QueueEvent(new HoldCompletedEvent(noteData));
        }

        public static void PublishHoldBroken(NoteData noteData)
        {
            Instance.QueueEvent(new HoldBrokenEvent(noteData));
        }

        public static void PublishComboChanged(int combo)
        {
            Instance.QueueEvent(new ComboChangedEvent(combo));
        }

        public static void PublishBpmChanged(float newBpm)
        {
            Instance.QueueEvent(new BpmChangedEvent(newBpm));
        }

        public static void PublishInputEvent(InputEvent inputEvent)
        {
            Instance.QueueEvent(new InputEventWrapper(inputEvent));
        }

        #endregion

        #region Event Subscription

        public static void SubscribeToNoteHit(Action<NoteData, Judge> handler)
        {
            OnNoteHit += handler;
            // Subscribed to OnNoteHit
        }

        public static void UnsubscribeFromNoteHit(Action<NoteData, Judge> handler)
        {
            OnNoteHit -= handler;
        }

        public static void SubscribeToNoteMissed(Action<NoteData> handler)
        {
            OnNoteMissed += handler;
            // Subscribed to OnNoteMissed
        }

        public static void UnsubscribeFromNoteMissed(Action<NoteData> handler)
        {
            OnNoteMissed -= handler;
        }

        public static void SubscribeToHoldStarted(Action<NoteData> handler)
        {
            OnHoldStarted += handler;
        }

        public static void UnsubscribeFromHoldStarted(Action<NoteData> handler)
        {
            OnHoldStarted -= handler;
        }

        public static void SubscribeToHoldCompleted(Action<NoteData> handler)
        {
            OnHoldCompleted += handler;
        }

        public static void UnsubscribeFromHoldCompleted(Action<NoteData> handler)
        {
            OnHoldCompleted -= handler;
        }

        public static void SubscribeToHoldBroken(Action<NoteData> handler)
        {
            OnHoldBroken += handler;
        }

        public static void UnsubscribeFromHoldBroken(Action<NoteData> handler)
        {
            OnHoldBroken -= handler;
        }

        public static void SubscribeToComboChanged(Action<int> handler)
        {
            OnComboChanged += handler;
        }

        public static void UnsubscribeFromComboChanged(Action<int> handler)
        {
            OnComboChanged -= handler;
        }

        public static void SubscribeToBpmChanged(Action<float> handler)
        {
            OnBpmChanged += handler;
        }

        public static void UnsubscribeFromBpmChanged(Action<float> handler)
        {
            OnBpmChanged -= handler;
        }

        public static void SubscribeToInputEvent(Action<InputEvent> handler)
        {
            OnInputEvent += handler;
        }

        public static void UnsubscribeFromInputEvent(Action<InputEvent> handler)
        {
            OnInputEvent -= handler;
        }

        #endregion

        private void QueueEvent(GameEvent gameEvent)
        {
            eventQueue.Enqueue(gameEvent);
        }

        private void ProcessEventQueue()
        {
            while (eventQueue.Count > 0)
            {
                var gameEvent = eventQueue.Dequeue();
                ProcessEvent(gameEvent);
                AddToHistory(gameEvent);
            }
        }

        private void ProcessEvent(GameEvent gameEvent)
        {
            // Debug logging removed for performance

            switch (gameEvent)
            {
                case NoteHitEvent e:
                    OnNoteHit?.Invoke(e.NoteData, e.Judge);
                    break;
                case NoteMissedEvent e:
                    OnNoteMissed?.Invoke(e.NoteData);
                    break;
                case HoldStartedEvent e:
                    OnHoldStarted?.Invoke(e.NoteData);
                    break;
                case HoldCompletedEvent e:
                    OnHoldCompleted?.Invoke(e.NoteData);
                    break;
                case HoldBrokenEvent e:
                    OnHoldBroken?.Invoke(e.NoteData);
                    break;
                case ComboChangedEvent e:
                    OnComboChanged?.Invoke(e.Combo);
                    break;
                case BpmChangedEvent e:
                    OnBpmChanged?.Invoke(e.NewBpm);
                    break;
                case InputEventWrapper e:
                    OnInputEvent?.Invoke(e.InputEvent);
                    break;
            }
        }

        private void AddToHistory(GameEvent gameEvent)
        {
            eventHistory.Add(gameEvent);
            if (eventHistory.Count > maxHistorySize)
            {
                eventHistory.RemoveAt(0);
            }
        }

        public List<GameEvent> GetEventHistory()
        {
            return new List<GameEvent>(eventHistory);
        }
    }

    #region Event Classes

    public abstract class GameEvent
    {
        public double Timestamp { get; private set; }
        
        protected GameEvent()
        {
            Timestamp = AudioSettings.dspTime;
        }
    }

    public class NoteHitEvent : GameEvent
    {
        public NoteData NoteData { get; private set; }
        public Judge Judge { get; private set; }
        
        public NoteHitEvent(NoteData noteData, Judge judge)
        {
            NoteData = noteData;
            Judge = judge;
        }
    }

    public class NoteMissedEvent : GameEvent
    {
        public NoteData NoteData { get; private set; }
        
        public NoteMissedEvent(NoteData noteData)
        {
            NoteData = noteData;
        }
    }

    public class HoldStartedEvent : GameEvent
    {
        public NoteData NoteData { get; private set; }
        
        public HoldStartedEvent(NoteData noteData)
        {
            NoteData = noteData;
        }
    }

    public class HoldCompletedEvent : GameEvent
    {
        public NoteData NoteData { get; private set; }
        
        public HoldCompletedEvent(NoteData noteData)
        {
            NoteData = noteData;
        }
    }

    public class HoldBrokenEvent : GameEvent
    {
        public NoteData NoteData { get; private set; }
        
        public HoldBrokenEvent(NoteData noteData)
        {
            NoteData = noteData;
        }
    }

    public class ComboChangedEvent : GameEvent
    {
        public int Combo { get; private set; }
        
        public ComboChangedEvent(int combo)
        {
            Combo = combo;
        }
    }

    public class BpmChangedEvent : GameEvent
    {
        public float NewBpm { get; private set; }
        
        public BpmChangedEvent(float newBpm)
        {
            NewBpm = newBpm;
        }
    }

    public class InputEventWrapper : GameEvent
    {
        public InputEvent InputEvent { get; private set; }
        
        public InputEventWrapper(InputEvent inputEvent)
        {
            InputEvent = inputEvent;
        }
    }

    #endregion
}

