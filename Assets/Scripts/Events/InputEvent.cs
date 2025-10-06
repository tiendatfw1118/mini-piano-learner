using UnityEngine;

namespace SpeedItUp.Events
{
    /// <summary>
    /// Represents an input event with all necessary data
    /// </summary>
    public class InputEvent
    {
        public InputType Type { get; private set; }
        public int Degree { get; private set; }
        public double Timestamp { get; private set; }
        public double Duration { get; private set; }
        public Vector2 Position { get; private set; }
        public float Pressure { get; private set; }

        public InputEvent(InputType type, int degree, double timestamp, double duration = 0.0, Vector2 position = default, float pressure = 1.0f)
        {
            Type = type;
            Degree = degree;
            Timestamp = timestamp;
            Duration = duration;
            Position = position;
            Pressure = pressure;
        }

        public override string ToString()
        {
            return $"{Type} - Degree: {Degree}, Time: {Timestamp:F6}, Duration: {Duration:F3}";
        }
    }

    /// <summary>
    /// Types of input events
    /// </summary>
    public enum InputType
    {
        Tap,           // Quick press and release
        Press,         // Key pressed down
        Release,       // Key released
        HoldStart,     // Hold note started
        HoldEnd,       // Hold note ended
        HoldBreak      // Hold note broken
    }
}

