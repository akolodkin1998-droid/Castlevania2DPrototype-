using Castlevania2D.Hub;
using Castlevania2D.Player;
using Castlevania2D.Save;
using UnityEngine;

namespace Castlevania2D.Environment
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SceneWarpTotemAnimator2D))]
    public sealed class SceneWarpTotem2D : MonoBehaviour
    {
        private const string PlayerObjectName = "Player_HeroKnight";
        private const string PromptResourcePath = "UI/InteractPrompt_F";

        [SerializeField] private string destinationSceneName;
        [SerializeField] private string arrivalTotemName;
        [SerializeField] [Min(0.1f)] private float interactionDistance = 2.4f;
        [SerializeField] private Vector3 promptLocalPosition = new Vector3(0f, 3.2f, 0f);
        [SerializeField] private Vector3 promptScale = new Vector3(0.48f, 0.48f, 1f);
        [SerializeField] [Min(0.01f)] private float promptPopDuration = 0.22f;
        [SerializeField] [Min(0f)] private float promptPopOffset = 0.2f;
        [SerializeField] private Vector3 arrivalPlayerOffset = new Vector3(1.6f, 0f, 0f);
        [SerializeField] private Sprite promptSprite;

        private SceneWarpTotemAnimator2D animator;
        private Transform player;
        private SpriteRenderer promptRenderer;
        private float nextPlayerSearchTime;
        private bool promptWanted;
        private float promptPop;
        private bool warpStarted;

        public static bool IsBusy { get; private set; }

        private void Awake()
        {
            animator = GetComponent<SceneWarpTotemAnimator2D>();
            if (animator != null)
            {
                animator.WarpAnimationCompleted += OnWarpAnimationCompleted;
            }

            CreatePrompt();
        }

        private void Start()
        {
            if (!SceneWarpSession.TryConsumeArrival(name))
            {
                return;
            }

            PlacePlayerBeside();
        }

        private void OnDisable()
        {
            if (animator != null)
            {
                animator.WarpAnimationCompleted -= OnWarpAnimationCompleted;
            }

            if (warpStarted)
            {
                IsBusy = false;
                warpStarted = false;
            }
        }

        private void Update()
        {
            CachePlayerIfNeeded();
            if (player == null || promptRenderer == null)
            {
                return;
            }

            bool isNear = ((Vector2)(player.position - transform.position)).sqrMagnitude
                          <= interactionDistance * interactionDistance;
            SaveLoadSessionController session = SaveLoadSessionController.Instance;
            bool canInteract = isNear
                               && !IsBusy
                               && !HubMerchantTalk2D.BlocksHubInteract
                               && (session == null || session.CanInteract)
                               && !string.IsNullOrEmpty(destinationSceneName);

            promptWanted = canInteract && !warpStarted;
            TickPromptPop();

            if (!canInteract || warpStarted || !UnityEngine.Input.GetKeyDown(KeyCode.F))
            {
                return;
            }

            if (animator == null || !animator.PlayWarpAnimation())
            {
                BeginSceneLoad();
                return;
            }

            warpStarted = true;
            IsBusy = true;
            promptWanted = false;
        }

        private void OnWarpAnimationCompleted()
        {
            BeginSceneLoad();
        }

        private void BeginSceneLoad()
        {
            SceneWarpSession.MarkArrival(arrivalTotemName);
            HubSceneFadeLoad.Load(destinationSceneName);
        }

        private void PlacePlayerBeside()
        {
            CachePlayerIfNeeded();
            if (player == null)
            {
                return;
            }

            Vector3 worldPosition = transform.position + arrivalPlayerOffset;
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.position = worldPosition;
            }

            player.position = worldPosition;
            CameraFollow2D follow = Camera.main != null
                ? Camera.main.GetComponent<CameraFollow2D>()
                : null;
            follow?.SnapNow();
        }

        private void CachePlayerIfNeeded()
        {
            if (player != null || Time.unscaledTime < nextPlayerSearchTime)
            {
                return;
            }

            nextPlayerSearchTime = Time.unscaledTime + 1f;
            GameObject playerObject = GameObject.Find(PlayerObjectName);
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }

        private void CreatePrompt()
        {
            Sprite sprite = promptSprite != null
                ? promptSprite
                : Resources.Load<Sprite>(PromptResourcePath);

            var promptObject = new GameObject("InteractionPrompt");
            promptObject.transform.SetParent(transform, false);
            promptObject.transform.localPosition = promptLocalPosition;

            promptRenderer = promptObject.AddComponent<SpriteRenderer>();
            promptRenderer.sprite = sprite;
            promptRenderer.sortingOrder = 20;
            ApplyPromptVisual(0f);
        }

        private void TickPromptPop()
        {
            float target = promptWanted ? 1f : 0f;
            float speed = promptPopDuration > 0.001f ? 1f / promptPopDuration : 1000f;
            promptPop = Mathf.MoveTowards(promptPop, target, speed * Time.unscaledDeltaTime);
            ApplyPromptVisual(promptPop * promptPop * (3f - 2f * promptPop));
        }

        private void ApplyPromptVisual(float eased)
        {
            if (promptRenderer == null)
            {
                return;
            }

            Transform promptTransform = promptRenderer.transform;
            promptTransform.localScale = promptScale * eased;
            promptTransform.localPosition = promptLocalPosition
                                           + new Vector3(0f, (eased - 1f) * promptPopOffset, 0f);

            Color color = Color.white;
            color.a = eased;
            promptRenderer.color = color;
            promptRenderer.enabled = eased > 0.001f && promptRenderer.sprite != null;
        }

#if UNITY_EDITOR
        public void EditorAssign(
            string destinationScene,
            string arrivalName,
            Sprite prompt)
        {
            destinationSceneName = destinationScene;
            arrivalTotemName = arrivalName;
            promptSprite = prompt;
        }
#endif
    }
}
