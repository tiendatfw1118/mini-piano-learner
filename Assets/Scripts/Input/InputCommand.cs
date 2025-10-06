using UnityEngine;

namespace SpeedItUp.Input
{
    /// <summary>
    /// Base interface for all input commands
    /// </summary>
    public interface IInputCommand
    {
        void Execute();
        bool CanExecute();
        double GetTimestamp();
        int GetDegree();
    }

    /// <summary>
    /// Command for regular note hits
    /// </summary>
    public class HitNoteCommand : IInputCommand
    {
        private readonly NoteData noteData;
        private readonly double timestamp;
        private readonly int degree;
        private readonly double distance;
        private readonly JudgeController judgeController;

        public HitNoteCommand(NoteData noteData, double timestamp, int degree, double distance, JudgeController judgeController)
        {
            this.noteData = noteData;
            this.timestamp = timestamp;
            this.degree = degree;
            this.distance = distance;
            this.judgeController = judgeController;
        }

        public void Execute()
        {
            if (CanExecute())
            {
                // All notes are now hold notes - redirect to hold note processing
                judgeController.ProcessHoldNoteStart(noteData, distance, timestamp);
            }
        }

        public bool CanExecute()
        {
            return noteData != null && !noteData.headHit && !noteData.IsHoldNote;
        }

        public double GetTimestamp() => timestamp;
        public int GetDegree() => degree;
    }

    /// <summary>
    /// Command for starting hold notes
    /// </summary>
    public class StartHoldCommand : IInputCommand
    {
        private readonly NoteData noteData;
        private readonly double timestamp;
        private readonly int degree;
        private readonly double tailDistance;
        private readonly JudgeController judgeController;

        public StartHoldCommand(NoteData noteData, double timestamp, int degree, double tailDistance, JudgeController judgeController)
        {
            this.noteData = noteData;
            this.timestamp = timestamp;
            this.degree = degree;
            this.tailDistance = tailDistance;
            this.judgeController = judgeController;
        }

        public void Execute()
        {
            if (CanExecute())
            {
                judgeController.ProcessHoldNoteStart(noteData, tailDistance, timestamp);
            }
        }

        public bool CanExecute()
        {
            return noteData != null && !noteData.headHit && noteData.IsHoldNote;
        }

        public double GetTimestamp() => timestamp;
        public int GetDegree() => degree;
    }

    /// <summary>
    /// Command for releasing hold notes
    /// </summary>
    public class ReleaseHoldCommand : IInputCommand
    {
        private readonly NoteData noteData;
        private readonly double timestamp;
        private readonly int degree;
        private readonly JudgeController judgeController;

        public ReleaseHoldCommand(NoteData noteData, double timestamp, int degree, JudgeController judgeController)
        {
            this.noteData = noteData;
            this.timestamp = timestamp;
            this.degree = degree;
            this.judgeController = judgeController;
        }

        public void Execute()
        {
            if (CanExecute())
            {
                judgeController.ProcessHoldNoteRelease(noteData, timestamp);
            }
        }

        public bool CanExecute()
        {
            return noteData != null && noteData.headHit && noteData.IsHoldNote && noteData.isBeingHeld;
        }

        public double GetTimestamp() => timestamp;
        public int GetDegree() => degree;
    }

    /// <summary>
    /// Command for miss events
    /// </summary>
    public class MissCommand : IInputCommand
    {
        private readonly double timestamp;
        private readonly int degree;
        private readonly JudgeController judgeController;

        public MissCommand(double timestamp, int degree, JudgeController judgeController)
        {
            this.timestamp = timestamp;
            this.degree = degree;
            this.judgeController = judgeController;
        }

        public void Execute()
        {
            judgeController.ProcessMiss(degree, timestamp);
        }

        public bool CanExecute() => true;
        public double GetTimestamp() => timestamp;
        public int GetDegree() => degree;
    }
}
