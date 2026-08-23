namespace Castlevania2D.Save
{
    public interface IBossSaveState
    {
        bool HasStartedAttackSequence { get; }
        bool HasDisappeared { get; }
        void ApplySavedState(bool savedAttackStarted, bool savedDefeated);
    }
}
