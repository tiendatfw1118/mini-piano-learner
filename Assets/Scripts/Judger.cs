public enum Judge { Perfect, Great, Good, Miss }

public static class Judger
{
    public static Judge JudgeAt(double expectedDspTime, double inputDspTime, RemoteConfigData cfg)
    {
        double deltaMs = (inputDspTime - expectedDspTime) * 1000.0 - cfg.inputOffsetMs;
        double ad = System.Math.Abs(deltaMs);
        if (ad <= cfg.hitWindowMs.perfect) return Judge.Perfect;
        if (ad <= cfg.hitWindowMs.great)   return Judge.Great;
        if (ad <= cfg.hitWindowMs.good)    return Judge.Good;
        return Judge.Miss;
    }
}
