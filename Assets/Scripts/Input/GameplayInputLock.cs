using UnityEngine;

namespace Castlevania2D.Input
{
    public static class GameplayInputLock
    {
        public static bool IsLocked { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void UnlockAfterSceneLoad()
        {
            IsLocked = false;
        }
    }
}
