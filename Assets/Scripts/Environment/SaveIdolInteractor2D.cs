using Castlevania2D.Input;
using Castlevania2D.Loot;
using Castlevania2D.Save;
using Castlevania2D.UI;
using UnityEngine;

namespace Castlevania2D.Environment
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SaveIdolAnimator2D))]
    public sealed class SaveIdolInteractor2D : MonoBehaviour
    {
        private const string PlayerObjectName = "Player_HeroKnight";
        private const string NoTearText = "Нужна Слеза Мары";
        private const string SaveHintId = "save_idol";
        private const string SaveHintTitle = "Сохранение";
        private const string SaveHintText =
            "Здесь можно сохранить игру. Нажми F. Каждое сохранение расходует Слезу Мары — относись к сохранениям с умом.";

        [SerializeField] [Min(0.1f)] private float interactionDistance = 2f;
        [SerializeField] private Vector3 messageLocalPosition = new Vector3(0f, 3.4f, 0f);
        [SerializeField] [Min(0.1f)] private float messageDuration = 1.5f;

        private SaveIdolAnimator2D idolAnimator;
        private Transform player;
        private PlayerLootInventory inventory;
        private TextMesh messageText;
        private float nextPlayerSearchTime;
        private float messageVisibleUntil;

        private void Awake()
        {
            idolAnimator = GetComponent<SaveIdolAnimator2D>();
            DestroyLegacyPrompt();
            CreateMessage();
            EnsureSaveHint();
        }

        private void Update()
        {
            CachePlayerIfNeeded();
            if (player == null)
            {
                return;
            }

            bool isNear = ((Vector2)(player.position - transform.position)).sqrMagnitude
                          <= interactionDistance * interactionDistance;
            SaveLoadSessionController controller = SaveLoadSessionController.Instance;
            bool canInteract = isNear
                               && controller != null
                               && controller.CanInteract
                               && !GameplayInputLock.IsLocked
                               && !HintScrollUI.IsOpen
                               && !DialogueBoxUI.IsOpen;
            bool showMessage = Time.unscaledTime < messageVisibleUntil;
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

        private void DestroyLegacyPrompt()
        {
            Transform existing = transform.Find("InteractionPrompt");
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }
        }

        private void CreateMessage()
        {
            Transform existing = transform.Find("InteractionMessage");
            GameObject messageObject = existing != null
                ? existing.gameObject
                : new GameObject("InteractionMessage", typeof(TextMesh));
            if (existing == null)
            {
                messageObject.transform.SetParent(transform, false);
            }

            messageObject.transform.localPosition = messageLocalPosition;

            messageText = messageObject.GetComponent<TextMesh>();
            if (messageText == null)
            {
                messageText = messageObject.AddComponent<TextMesh>();
            }

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

        private void EnsureSaveHint()
        {
            ParchmentHintTrigger2D hint = GetComponent<ParchmentHintTrigger2D>();
            if (hint == null)
            {
                hint = gameObject.AddComponent<ParchmentHintTrigger2D>();
            }

            hint.Configure(
                SaveHintId,
                SaveHintTitle,
                SaveHintText,
                interactionDistance);
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
        }
#endif
    }
}
