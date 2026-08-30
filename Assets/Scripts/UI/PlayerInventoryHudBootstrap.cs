using Castlevania2D.Hub;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Castlevania2D.UI
{
    /// <summary>
    /// One hold-I inventory canvas for Prototype and Hub. Survives scene loads
    /// so the panel is not lost when the previous scene's HUD is destroyed.
    /// </summary>
    public static class PlayerInventoryHudBootstrap
    {
        private static PlayerInventoryHudView view;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Refresh();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Refresh();
        }

        public static void Refresh()
        {
            Scene scene = SceneManager.GetActiveScene();
            bool gameplay = scene.IsValid()
                            && (scene.name == "Prototype" || scene.name == HubTaflSession.HubSceneName);

            if (!gameplay)
            {
                if (view != null)
                {
                    view.gameObject.SetActive(false);
                }

                return;
            }

            EnsureHud();
            if (view == null)
            {
                return;
            }

            view.gameObject.SetActive(true);
            view.Rebind();
        }

        private static void EnsureHud()
        {
            if (view != null)
            {
                return;
            }

            var root = new GameObject(
                "PlayerInventoryCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(PlayerInventoryHudView));
            Object.DontDestroyOnLoad(root);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            view = root.GetComponent<PlayerInventoryHudView>();
        }
    }
}
