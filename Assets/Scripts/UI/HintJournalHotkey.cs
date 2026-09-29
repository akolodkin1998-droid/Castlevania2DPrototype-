using Castlevania2D.Input;
using UnityEngine;

namespace Castlevania2D.UI
{
    /// <summary>
    /// Opens the hint journal on ё / ~.
    /// </summary>
    public sealed class HintJournalHotkey : MonoBehaviour
    {
        private static HintJournalHotkey instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (instance != null)
            {
                return;
            }

            var root = new GameObject("HintJournalHotkey");
            Object.DontDestroyOnLoad(root);
            instance = root.AddComponent<HintJournalHotkey>();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Update()
        {
            if (!UnityEngine.Input.GetKeyDown(KeyCode.BackQuote)
                && !UnityEngine.Input.GetKeyDown(KeyCode.Tilde))
            {
                return;
            }

            if (DialogueBoxUI.IsOpen)
            {
                return;
            }

            if (GameplayInputLock.IsLocked && !HintScrollUI.IsOpen)
            {
                return;
            }

            HintScrollUI.ToggleJournal();
        }
    }
}
