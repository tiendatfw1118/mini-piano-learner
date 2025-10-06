using UnityEngine;

namespace SpeedItUp.Strategies
{
    /// <summary>
    /// Strategy interface for processing different types of notes
    /// </summary>
    public interface INoteProcessingStrategy
    {
        bool CanProcess(NoteData noteData);
        void Process(NoteData noteData, double currentBeat, double hitWindow);
        Judge CalculateJudge(NoteData noteData, double distance);
    }

    /// <summary>
    /// Strategy for processing regular notes
    /// </summary>
    public class RegularNoteStrategy : INoteProcessingStrategy
    {
        private readonly JudgeController judgeController;
        private readonly RemoteConfigData config;

        public RegularNoteStrategy(JudgeController judgeController, RemoteConfigData config)
        {
            this.judgeController = judgeController;
            this.config = config;
        }

        public bool CanProcess(NoteData noteData)
        {
            // All notes are now hold notes, so this strategy is no longer used
            return false;
        }

        public void Process(NoteData noteData, double currentBeat, double hitWindow)
        {
            if (!CanProcess(noteData)) return;

            double distance = System.Math.Abs(noteData.targetBeat - currentBeat);
            if (distance <= hitWindow)
            {
                Judge judge = CalculateJudge(noteData, distance);
                // All notes are now hold notes - redirect to hold note processing
                judgeController.ProcessHoldNoteStart(noteData, distance, currentBeat);
            }
        }

        public Judge CalculateJudge(NoteData noteData, double distance)
        {
            double perfectWindow = (config.hitWindowMs.perfect / 1000.0) * (judgeController.conductor.bpm / 60.0);
            double greatWindow = (config.hitWindowMs.great / 1000.0) * (judgeController.conductor.bpm / 60.0);
            double goodWindow = (config.hitWindowMs.good / 1000.0) * (judgeController.conductor.bpm / 60.0);

            if (distance <= perfectWindow) return Judge.Perfect;
            if (distance <= greatWindow) return Judge.Great;
            if (distance <= goodWindow) return Judge.Good;
            return Judge.Miss;
        }
    }

    /// <summary>
    /// Strategy for processing hold notes
    /// </summary>
    public class HoldNoteStrategy : INoteProcessingStrategy
    {
        private readonly JudgeController judgeController;
        private readonly RemoteConfigData config;

        public HoldNoteStrategy(JudgeController judgeController, RemoteConfigData config)
        {
            this.judgeController = judgeController;
            this.config = config;
        }

        public bool CanProcess(NoteData noteData)
        {
            return noteData != null && noteData.IsHoldNote;
        }

        public void Process(NoteData noteData, double currentBeat, double hitWindow)
        {
            if (!CanProcess(noteData)) return;

            if (!noteData.headHit)
            {
                // Check if we should start the hold (tail is in hit window)
                double tailBeat = noteData.targetBeat - noteData.durationBeats;
                double tailDistance = System.Math.Abs(tailBeat - currentBeat);
                
                if (tailDistance <= hitWindow)
                {
                    Judge judge = CalculateJudge(noteData, tailDistance);
                    judgeController.ProcessHoldNoteStart(noteData, tailDistance, currentBeat);
                }
            }
            else if (noteData.isBeingHeld)
            {
                // Check if hold should be completed (head reached hit line)
                double headDistance = System.Math.Abs(noteData.targetBeat - currentBeat);
                if (headDistance <= hitWindow)
                {
                    judgeController.ProcessHoldNoteComplete(noteData, currentBeat);
                }
            }
        }

        public Judge CalculateJudge(NoteData noteData, double distance)
        {
            double perfectWindow = (config.hitWindowMs.perfect / 1000.0) * (judgeController.conductor.bpm / 60.0);
            double greatWindow = (config.hitWindowMs.great / 1000.0) * (judgeController.conductor.bpm / 60.0);
            double goodWindow = (config.hitWindowMs.good / 1000.0) * (judgeController.conductor.bpm / 60.0);

            if (distance <= perfectWindow) return Judge.Perfect;
            if (distance <= greatWindow) return Judge.Great;
            if (distance <= goodWindow) return Judge.Good;
            return Judge.Miss;
        }
    }

    /// <summary>
    /// Context class that uses the strategy pattern
    /// </summary>
    public class NoteProcessor
    {
        private readonly INoteProcessingStrategy regularNoteStrategy;
        private readonly INoteProcessingStrategy holdNoteStrategy;

        public NoteProcessor(JudgeController judgeController, RemoteConfigData config)
        {
            regularNoteStrategy = new RegularNoteStrategy(judgeController, config);
            holdNoteStrategy = new HoldNoteStrategy(judgeController, config);
        }

        public void ProcessNote(NoteData noteData, double currentBeat, double hitWindow)
        {
            if (noteData == null) return;

            if (noteData.IsHoldNote)
            {
                holdNoteStrategy.Process(noteData, currentBeat, hitWindow);
            }
            else
            {
                regularNoteStrategy.Process(noteData, currentBeat, hitWindow);
            }
        }

        public Judge CalculateJudge(NoteData noteData, double distance)
        {
            if (noteData == null) return Judge.Miss;

            if (noteData.IsHoldNote)
            {
                return holdNoteStrategy.CalculateJudge(noteData, distance);
            }
            else
            {
                return regularNoteStrategy.CalculateJudge(noteData, distance);
            }
        }
    }
}
