using UnityEngine;

namespace Castlevania2D.Input
{
    public static class GameplayPause
    {
        private static int holdCount;
        private static float scaleBeforePause = 1f;

        public static bool IsPaused => holdCount > 0;

        public static void Hold()
        {
            if (holdCount == 0)
            {
                scaleBeforePause = Time.timeScale;
                Time.timeScale = 0f;
            }

            holdCount++;
        }

        public static void Release()
        {
            if (holdCount <= 0)
            {
                return;
            }

            holdCount--;
            if (holdCount > 0)
            {
                return;
            }

            float restore = scaleBeforePause;
            if (restore <= 0.0001f)
            {
                restore = 1f;
            }

            Time.timeScale = restore;
        }
    }
}
