namespace Castlevania2D.Hub
{
    public static class HubTaflSession
    {
        public const string HubSceneName = "Hub";
        public const string TaflSceneName = "Hnefatafl";

        public static bool ReturnToShop { get; private set; }

        public static void MarkReturnToShop()
        {
            ReturnToShop = true;
        }

        public static bool ConsumeReturnToShop()
        {
            bool value = ReturnToShop;
            ReturnToShop = false;
            return value;
        }
    }
}
