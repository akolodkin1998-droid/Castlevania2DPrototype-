using Castlevania2D.Loot;
using Castlevania2D.Save;
using UnityEngine;

namespace Castlevania2D.Environment
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SaveIdolAnimator2D))]
    public sealed class SaveIdolInteractor2D : MonoBehaviour
    {
        private const string PlayerObjectName = "Player_HeroKnight";
        private const string NoTearText = "Нужна Слеза Мары";
        private const string PromptResourcePath = "UI/InteractPrompt_F";

        [SerializeField] [Min(0.1f)] private float interactionDistance = 2f;
        [SerializeField] private Vector3 promptLocalPosition = new Vector3(0f, 2.55f, 0f);
        [SerializeField] private Vector3 promptScale = new Vector3(0.48f, 0.48f, 1f);
        [SerializeField] [Min(0.01f)] private float promptPopDuration = 0.22f;
        [SerializeField] [Min(0f)] private float promptPopOffset = 0.2f;
        [SerializeField] [Min(0.1f)] private float messageDuration = 1.5f;
        [SerializeField] private Sprite promptSprite;

        private SaveIdolAnimator2D idolAnimator;
        private Transform player;
        private PlayerLootInventory inventory;
        private SpriteRenderer promptRenderer;
        private TextMesh messageText;
        private float nextPlayerSearchTime;
        private float messageVisibleUntil;
        private bool promptWanted;
        private float promptPop;

        private void Awake()
        {
            idolAnimator = GetComponent<SaveIdolAnimator2D>();
            CreatePrompt();
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
            SaveLoadSessionController controller = SaveLoadSessionController.Instance;
            bool canInteract = isNear && controller != null && controller.CanInteract;
            bool showMessage = Time.unscaledTime < messageVisibleUntil;

            promptWanted = canInteract && !showMessage;
            TickPromptPop();
            SetMessageVisible(showMessage);

            if (showMessage)
            {
                return;
            }

            if (!canInteract || !UnityEngine.Input.GetKeyDown(KeyCode.F))
            {
                return;
            }

            if (inventory == null)
            {
                inventory = player.GetComponent<PlayerLootInventory>();
            }

            if (inventory == null || inventory.MaraTearCount <= 0)
            {
                messageVisibleUntil = Time.unscaledTime + messageDuration;
                if (messageText != null)
                {
                    messageText.text = NoTearText;
                }

                promptWanted = false;
                SetMessageVisible(true);
                return;
            }

            if (!inventory.TryRemove(LootItemId.MaraTear))
            {
                return;
            }

            if (!controller.TryOpenFromIdol(idolAnimator))
            {
                inventory.Add(LootItemId.MaraTear);
                return;
            }

            promptWanted = false;
            SetMessageVisible(false);
        }

        private void CachePlayerIfNeeded()
        {
            if (player != null || Time.unscaledTime < nextPlayerSearchTime)
            {
                return;
            }

            nextPlayerSearchTime = Time.unscaledTime + 1f;
            GameObject playerObject = GameObject.Find(PlayerObjectName);
            if (playerObject == null)
            {
                return;
            }

            player = playerObject.transform;
            inventory = playerObject.GetComponent<PlayerLootInventory>();
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

            var messageObject = new GameObject("InteractionMessage", typeof(TextMesh));
            messageObject.transform.SetParent(transform, false);
            messageObject.transform.localPosition = promptLocalPosition + new Vector3(0f, 0.85f, 0f);

            messageText = messageObject.GetComponent<TextMesh>();
            messageText.text = NoTearText;
            messageText.anchor = TextAnchor.LowerCenter;
            messageText.alignment = TextAlignment.Center;
            messageText.fontSize = 32;
            messageText.characterSize = 0.08f;
            messageText.color = new Color(0.95f, 0.87f, 0.68f, 1f);

            MeshRenderer renderer = messageObject.GetComponent<MeshRenderer>();
            renderer.sortingOrder = 20;
            messageObject.SetActive(false);
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

        private void SetMessageVisible(bool visible)
        {
            if (messageText != null && messageText.gameObject.activeSelf != visible)
            {
                messageText.gameObject.SetActive(visible);
            }
        }

#if UNITY_EDITOR
        public void EditorAssignPrompt(Sprite sprite)
        {
            promptSprite = sprite;
        }
#endif
    }
}
