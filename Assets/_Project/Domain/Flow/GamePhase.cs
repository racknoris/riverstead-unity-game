namespace Contraption.Domain.Flow
{
    /// <summary>
    /// The phases of the build -> run -> succeed/fail -> modify loop.
    /// Owned by the domain layer; the view layer reads it and never assigns it.
    /// </summary>
    public enum GamePhase
    {
        Editing,
        Running,
        Paused,
        Completed,
        Failed
    }
}
