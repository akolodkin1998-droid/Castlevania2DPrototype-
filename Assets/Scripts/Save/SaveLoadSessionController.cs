using System.Collections;
using Castlevania2D.Environment;
using Castlevania2D.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using PlayerHealth = Castlevania2D.Health.Health;

namespace Castlevania2D.Save
{
    public sealed class SaveLoadSessionController : MonoBehaviour
    {
        private const string PrototypeSceneName = "Prototype";
        private const string MenuSceneName = "SaveLoadMenu";
        private const string MainMenuSceneName = "MainMenu";
        private const string PlayerObjectName = "Player_HeroKnight";
        private const string EmptySlotText = "Пустой слот";
        private const float TransitionFadeDuration = 0.5f;
        private const float PlayerDeathAnimationDuration = 1.15f;

        private static SaveLoadSessionController instance;

        private SaveIdolAnimator2D activeIdol;
        private SaveLoadMenuView menuView;
        private MainMenuView mainMenuView;
        private PlayerHealth playerHealth;
        private ScreenFadeOverlay fadeOverlay;
        private bool transitionInProgress;
        private bool menuOpen;
        private bool menuOpenedAfterDeath;
        private float previousTimeScale = 1f;

        public static SaveLoadSessionController Instance => instance;
        public bool CanInteract => !transitionInProgress && !menuOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateRuntimeController()
        {
            if (instance != null)
            {
                return;
            }

            var controllerObject = new GameObject(nameof(SaveLoadSessionController));
            instance = controllerObject.AddComponent<SaveLoadSessionController>();
            DontDestroyOnLoad(controllerObject);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            fadeOverlay = ScreenFadeOverlay.Create(transform);
            SceneManager.sceneLoaded += OnSceneLoaded;

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isLoaded)
            {
                OnSceneLoaded(activeScene, LoadSceneMode.Single);
            }
        }

        private void OnDestroy()
        {
            if (instance != this)
            {
                return;
            }

            UnsubscribeFromIdol();
            UnsubscribeFromMenu();
            UnsubscribeFromMainMenu();
            UnsubscribeFromPlayerHealth();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Time.timeScale = previousTimeScale;
            instance = null;
        }

        public bool TryOpenFromIdol(SaveIdolAnimator2D idol)
        {
            if (!CanInteract
                || idol == null
                || SceneManager.GetActiveScene().name != PrototypeSceneName)
            {
                return false;
            }

            transitionInProgress = true;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            activeIdol = idol;
            activeIdol.SaveAnimationCompleted += OnIdolAnimationCompleted;

            if (activeIdol.PlaySaveAnimation())
            {
                return true;
            }

            UnsubscribeFromIdol();
            Time.timeScale = previousTimeScale;
            transitionInProgress = false;
            return false;
        }

        private void OnIdolAnimationCompleted()
        {
            UnsubscribeFromIdol();
            StartCoroutine(OpenMenuRoutine(openedAfterDeath: false));
        }

        private IEnumerator OpenMenuRoutine(bool openedAfterDeath)
        {
            yield return fadeOverlay.FadeTo(1f, TransitionFadeDuration);

            if (!SceneManager.GetSceneByName(MenuSceneName).isLoaded)
            {
                AsyncOperation load = SceneManager.LoadSceneAsync(MenuSceneName, LoadSceneMode.Additive);
                if (load == null)
                {
                    yield return fadeOverlay.FadeTo(0f, TransitionFadeDuration);
                    ResumeWorldAfterFailure();
                    yield break;
                }

                yield return load;
            }

            menuView = FindMenuView();
            if (menuView == null)
            {
                Debug.LogError("[SaveLoadSessionController] SaveLoadMenuView was not found.");
                yield return fadeOverlay.FadeTo(0f, TransitionFadeDuration);
                ResumeWorldAfterFailure();
                yield break;
            }

            menuOpenedAfterDeath = openedAfterDeath;
            menuView.SetDeathMode(openedAfterDeath);
            SubscribeToMenu();
            RefreshSlotLabels();
            menuOpen = true;
            yield return fadeOverlay.FadeTo(0f, TransitionFadeDuration);
            transitionInProgress = false;
        }

        private void SubscribeToMenu()
        {
            menuView.SaveRequested += OnSaveRequested;
            menuView.LoadRequested += OnLoadRequested;
            menuView.CloseRequested += OnCloseRequested;
        }

        private void UnsubscribeFromMenu()
        {
            if (menuView == null)
            {
                return;
            }

            menuView.SaveRequested -= OnSaveRequested;
            menuView.LoadRequested -= OnLoadRequested;
            menuView.CloseRequested -= OnCloseRequested;
            menuView = null;
        }

        private void UnsubscribeFromIdol()
        {
            if (activeIdol == null)
            {
                return;
            }

            activeIdol.SaveAnimationCompleted -= OnIdolAnimationCompleted;
            activeIdol = null;
        }

        private void OnSaveRequested()
        {
            if (transitionInProgress || menuView == null || menuOpenedAfterDeath)
            {
                return;
            }

            int slotIndex = menuView.SelectedSlotIndex;
            if (!PrototypeSaveStateService.TryCapture(out GameSaveData data))
            {
                menuView.SetSlotText(slotIndex, "Ошибка сохранения");
                return;
            }

            if (!GameSaveFileStore.TryWrite(slotIndex, data))
            {
                menuView.SetSlotText(slotIndex, "Ошибка сохранения");
                return;
            }

            menuView.SetSlotText(slotIndex, data.timestamp);
        }

        private void OnLoadRequested()
        {
            if (transitionInProgress || menuView == null)
            {
                return;
            }

            int slotIndex = menuView.SelectedSlotIndex;
            if (!GameSaveFileStore.TryRead(slotIndex, out GameSaveData data))
            {
                menuView.SetSlotText(slotIndex, EmptySlotText);
                return;
            }

            StartCoroutine(LoadGameRoutine(data));
        }

        private void OnCloseRequested()
        {
            if (transitionInProgress)
            {
                return;
            }

            if (menuOpenedAfterDeath)
            {
                StartCoroutine(GoToMainMenuRoutine());
            }
            else
            {
                StartCoroutine(CloseMenuRoutine());
            }
        }

        private IEnumerator CloseMenuRoutine()
        {
            transitionInProgress = true;
            yield return fadeOverlay.FadeTo(1f, TransitionFadeDuration);
            UnsubscribeFromMenu();

            Scene menuScene = SceneManager.GetSceneByName(MenuSceneName);
            if (menuScene.IsValid() && menuScene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(menuScene);
            }

            menuOpen = false;
            menuOpenedAfterDeath = false;
            yield return fadeOverlay.FadeTo(0f, TransitionFadeDuration);
            Time.timeScale = previousTimeScale;
            transitionInProgress = false;
        }

        private IEnumerator LoadGameRoutine(GameSaveData data)
        {
            transitionInProgress = true;
            yield return fadeOverlay.FadeTo(1f, TransitionFadeDuration);

            UnsubscribeFromMenu();
            menuOpen = false;
            menuOpenedAfterDeath = false;

            AsyncOperation load = SceneManager.LoadSceneAsync(PrototypeSceneName, LoadSceneMode.Single);
            if (load == null)
            {
                yield return fadeOverlay.FadeTo(0f, TransitionFadeDuration);
                ResumeWorldAfterFailure();
                yield break;
            }

            yield return load;
            yield return null;
            yield return null;

            if (!PrototypeSaveStateService.Apply(data))
            {
                Debug.LogError("[SaveLoadSessionController] Saved state could not be applied.");
            }

            yield return fadeOverlay.FadeTo(0f, TransitionFadeDuration);
            Time.timeScale = previousTimeScale;
            transitionInProgress = false;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == PrototypeSceneName)
            {
                UnsubscribeFromMainMenu();
                StartCoroutine(BindPlayerHealthRoutine());
            }
            else if (scene.name == MainMenuSceneName)
            {
                UnsubscribeFromPlayerHealth();
                StartCoroutine(BindMainMenuRoutine(scene));
            }
        }

        private IEnumerator BindPlayerHealthRoutine()
        {
            yield return null;
            UnsubscribeFromPlayerHealth();

            GameObject player = GameObject.Find(PlayerObjectName);
            if (player == null)
            {
                yield break;
            }

            playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.Died += OnPlayerDied;
            }
        }

        private void UnsubscribeFromPlayerHealth()
        {
            if (playerHealth != null)
            {
                playerHealth.Died -= OnPlayerDied;
            }

            playerHealth = null;
        }

        private void OnPlayerDied()
        {
            if (transitionInProgress || menuOpen)
            {
                return;
            }

            transitionInProgress = true;
            StartCoroutine(OpenDeathMenuAfterAnimationRoutine());
        }

        private IEnumerator OpenDeathMenuAfterAnimationRoutine()
        {
            yield return new WaitForSecondsRealtime(PlayerDeathAnimationDuration);

            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            yield return OpenMenuRoutine(openedAfterDeath: true);
        }

        private IEnumerator GoToMainMenuRoutine()
        {
            transitionInProgress = true;
            yield return fadeOverlay.FadeTo(1f, TransitionFadeDuration);

            UnsubscribeFromMenu();
            UnsubscribeFromPlayerHealth();
            menuOpen = false;
            menuOpenedAfterDeath = false;

            AsyncOperation load =
                SceneManager.LoadSceneAsync(MainMenuSceneName, LoadSceneMode.Single);
            if (load == null)
            {
                yield return fadeOverlay.FadeTo(0f, TransitionFadeDuration);
                ResumeWorldAfterFailure();
                yield break;
            }

            yield return load;
            yield return null;
            yield return fadeOverlay.FadeTo(0f, TransitionFadeDuration);
            Time.timeScale = 1f;
            previousTimeScale = 1f;
            transitionInProgress = false;
        }

        private IEnumerator BindMainMenuRoutine(Scene scene)
        {
            yield return null;
            UnsubscribeFromMainMenu();

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                mainMenuView = roots[i].GetComponentInChildren<MainMenuView>(true);
                if (mainMenuView != null)
                {
                    mainMenuView.StartRequested += OnStartRequested;
                    yield break;
                }
            }

            mainMenuView = MainMenuRuntimeFactory.Create();
            mainMenuView.StartRequested += OnStartRequested;
        }

        private void UnsubscribeFromMainMenu()
        {
            if (mainMenuView != null)
            {
                mainMenuView.StartRequested -= OnStartRequested;
            }

            mainMenuView = null;
        }

        private void OnStartRequested()
        {
            if (!transitionInProgress)
            {
                StartCoroutine(StartNewGameRoutine());
            }
        }

        private IEnumerator StartNewGameRoutine()
        {
            transitionInProgress = true;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            yield return fadeOverlay.FadeTo(1f, TransitionFadeDuration);

            UnsubscribeFromMainMenu();
            AsyncOperation load =
                SceneManager.LoadSceneAsync(PrototypeSceneName, LoadSceneMode.Single);
            if (load == null)
            {
                yield return fadeOverlay.FadeTo(0f, TransitionFadeDuration);
                ResumeWorldAfterFailure();
                yield break;
            }

            yield return load;
            yield return null;
            yield return fadeOverlay.FadeTo(0f, TransitionFadeDuration);
            Time.timeScale = 1f;
            previousTimeScale = 1f;
            transitionInProgress = false;
        }

        private void RefreshSlotLabels()
        {
            if (menuView == null)
            {
                return;
            }

            int count = Mathf.Min(menuView.SlotCount, GameSaveFileStore.SlotCount);
            for (int i = 0; i < count; i++)
            {
                menuView.SetSlotText(
                    i,
                    GameSaveFileStore.TryGetTimestamp(i, out string timestamp)
                        ? timestamp
                        : EmptySlotText);
            }
        }

        private SaveLoadMenuView FindMenuView()
        {
            Scene menuScene = SceneManager.GetSceneByName(MenuSceneName);
            if (!menuScene.IsValid() || !menuScene.isLoaded)
            {
                return null;
            }

            GameObject[] roots = menuScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                SaveLoadMenuView view = roots[i].GetComponentInChildren<SaveLoadMenuView>(true);
                if (view != null)
                {
                    return view;
                }
            }

            return null;
        }

        private void ResumeWorldAfterFailure()
        {
            UnsubscribeFromIdol();
            UnsubscribeFromMenu();
            menuOpen = false;
            menuOpenedAfterDeath = false;
            transitionInProgress = false;
            Time.timeScale = previousTimeScale;
        }
    }
}
