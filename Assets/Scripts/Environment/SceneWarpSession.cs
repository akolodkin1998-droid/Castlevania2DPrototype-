namespace Castlevania2D.Environment
{
    public static class SceneWarpSession
    {
        public static string ArrivalTotemName { get; private set; }

        public static void MarkArrival(string totemName)
        {
            ArrivalTotemName = totemName;
        }

        public static bool TryConsumeArrival(string totemName)
        {
            if (string.IsNullOrEmpty(ArrivalTotemName) || ArrivalTotemName != totemName)
            {
                return false;
            }

            ArrivalTotemName = null;
            return true;
        }
    }
}
