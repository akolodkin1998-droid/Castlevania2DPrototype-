using System.Collections;
using Castlevania2D.Loot;
using Castlevania2D.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Castlevania2D.Hub
{
    public static class HubSceneFadeLoad
    {
        public static void Load(string sceneName, float fadeDuration = 0.45f)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return;
            }

            PlayerInventorySession.CaptureFromScene();

            var runnerObject = new GameObject(nameof(HubSceneFadeLoadRunner));
            Object.DontDestroyOnLoad(runnerObject);
            runnerObject.AddComponent<HubSceneFadeLoadRunner>().Begin(sceneName, fadeDuration);
        }
    }

    public sealed class HubSceneFadeLoadRunner : MonoBehaviour
    {
        public void Begin(string sceneName, float fadeDuration)
        {
            StartCoroutine(LoadRoutine(sceneName, fadeDuration));
        }

        private IEnumerator LoadRoutine(string sceneName, float fadeDuration)
        {
            ScreenFadeOverlay fade = ScreenFadeOverlay.Create(transform);
            yield return fade.FadeTo(1f, fadeDuration);

            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (load != null)
            {
                while (!load.isDone)
                {
                    yield return null;
                }
            }

            PlayerInventorySession.ApplyToScene();
            PlayerInventoryHudBootstrap.Refresh();

            yield return fade.FadeTo(0f, fadeDuration);
            Destroy(gameObject);
        }
    }
}
