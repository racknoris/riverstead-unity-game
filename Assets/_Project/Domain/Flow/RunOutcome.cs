namespace Contraption.Domain.Flow
{
    /// <summary>How a run ended. The "why" of a failure is carried separately as a reason.</summary>
    public enum RunOutcome
    {
        Completed,
        Failed
    }
}
