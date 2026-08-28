using Castlevania2D.Environment;
using Castlevania2D.Save;
using UnityEngine;

namespace Castlevania2D.Hub
{
    public sealed class HubDoorInteract2D : MonoBehaviour
    {
        private const string PlayerObjectName = "Player_HeroKnight";
        private const string PromptResourcePath = "UI/InteractPrompt_F";

        [SerializeField] private HubRoomTransition2D transition;
        [SerializeField] private Transform destination;
        [SerializeField] [Min(0.1f)] private float interactionDistance = 2.2f;
        [SerializeField] private Vector3 promptLocalPosition = new Vector3(0f, 2.4f, 0f);
        [SerializeField] private Vector3 promptScale = new Vector3(0.48f, 0.48f, 1f);
        [SerializeField] [Min(0.01f)] private float promptPopDuration = 0.22f;
        [SerializeField] [Min(0f)] private float promptPopOffset = 0.2f;
        [SerializeField] private Sprite promptSprite;
        [SerializeField] private bool enlargePlayer;

        private Transform player;
        private SpriteRenderer promptRenderer;
        private float nextPlayerSearchTime;
        private bool promptWanted;
        private float promptPop;

        private void Awake()
        {
            CreatePrompt();
        }

        private void Update()
        {
            CachePlayerIfNeeded();
            if (player == null || promptRenderer == null || destination == null || transition == null)
            {
                return;
            }

            bool isNear = ((Vector2)(player.position - transform.position)).sqrMagnitude
                          <= interactionDistance * interactionDistance;
            SaveLoadSessionController controller = SaveLoadSessionController.Instance;
            bool canInteract = isNear
                               && !transition.IsBusy
                               && !HubMerchantTalk2D.BlocksHubInteract
                               && !SceneWarpTotem2D.IsBusy
                               && (controller == null || controller.CanInteract);

            promptWanted = canInteract;
            TickPromptPop();

            if (!canInteract || !UnityEngine.Input.GetKeyDown(KeyCode.F))
            {
                return;
            }

            HubRoomView2D roomView = destination.GetComponentInParent<HubRoomView2D>();
            bool scaleUp = enlargePlayer || roomView != null;
            transition.TravelTo(destination.position, roomView, scaleUp);
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
        public void EditorAssign(HubRoomTransition2D roomTransition, Transform travelDestination, Sprite sprite)
        {
            transition = roomTransition;
            destination = travelDestination;
            promptSprite = sprite;
            enlargePlayer = travelDestination != null
                            && travelDestination.GetComponentInParent<HubRoomView2D>() != null;
        }
#endif
    }
}
