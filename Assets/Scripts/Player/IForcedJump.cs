namespace Castlevania2D.Player
{
    /// <summary>
    /// Allows items/VFX to force a jump even while airborne.
    /// </summary>
    public interface IForcedJump
    {
        void ForceJump(float forceMultiplier = 1f);
    }
}
