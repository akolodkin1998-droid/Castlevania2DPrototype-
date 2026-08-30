using Castlevania2D.Loot;

namespace Castlevania2D.Hub
{
    public static class HubTaflSession
    {
        public const string HubSceneName = "Hub";
        public const string TaflSceneName = "Hnefatafl";
        public const string PlayerObjectName = PlayerInventorySession.PlayerObjectName;

        public static bool ReturnToShop { get; private set; }

        public static int Coins => PlayerInventorySession.CommonCount;

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

        public static void CaptureCoinsFromPlayer()
        {
            PlayerInventorySession.CaptureFromScene();
        }

        public static void ApplyCoinsToPlayer()
        {
            PlayerInventorySession.ApplyToScene();
        }

        public static bool TrySpend(int amount)
        {
            return PlayerInventorySession.TrySpendCommon(amount);
        }

        public static void AddCoins(int amount)
        {
            PlayerInventorySession.AddCommon(amount);
        }
    }
}
