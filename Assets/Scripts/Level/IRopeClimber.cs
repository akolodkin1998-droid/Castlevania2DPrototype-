namespace Castlevania2D.Level
{
    public interface IRopeClimber
    {
        void NotifyRopeTouch(ClimbableRope2D rope);
        void NotifyRopeLeave(ClimbableRope2D rope);
    }
}
