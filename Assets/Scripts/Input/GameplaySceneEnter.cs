using Castlevania2D.UI;
using UnityEngine;

namespace Castlevania2D.Input
{
    /// <summary>
    /// Clears leftover UI/input statics after a scene load.
    /// Intro/Hub fades must not leave F-interact and loot pickup blocked.
    /// </summary>
    public static class GameplaySceneEnter
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ResetLeftovers()
        {
            Apply();
        }

        public static void Apply()
        {
            Time.timeScale = 1f;
            GameplayInputLock.IsLocked = false;
            GameplayPause.ForceClear();
            DialogueBoxUI.CloseIfOpen();
            HintScrollUI.CloseIfOpen();
        }
    }
}
