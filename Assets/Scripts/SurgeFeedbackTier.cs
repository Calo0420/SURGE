using SurgeCore;

public enum SurgeFeedbackTier
{
    Standard,
    Strong,
    Exceptional
}

public static class SurgeFeedback
{
    public static SurgeFeedbackTier Classify(ClearResult result)
    {
        if (result == null)
            return SurgeFeedbackTier.Standard;

        int pathLength = result.Path?.Length ?? 0;
        if (result.Purge || pathLength >= 7 || result.ChainMult >= 4)
            return SurgeFeedbackTier.Exceptional;

        if (pathLength >= 5 || result.ChainMult >= 2)
            return SurgeFeedbackTier.Strong;

        return SurgeFeedbackTier.Standard;
    }
}
