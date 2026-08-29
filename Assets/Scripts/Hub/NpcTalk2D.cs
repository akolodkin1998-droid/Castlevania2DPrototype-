using System;
using Castlevania2D.Environment;
using Castlevania2D.Save;
using Castlevania2D.UI;
using UnityEngine;

namespace Castlevania2D.Hub
{
    [DisallowMultipleComponent]
    public sealed class NpcTalk2D : MonoBehaviour
    {
        private const string PlayerObjectName = "Player_HeroKnight";
        private const string PromptResourcePath = "UI/InteractPrompt_F";

        [SerializeField] private string speakerName = "NPC";
        [SerializeField] [TextArea(2, 6)] private string line;
        [SerializeField] private string[] choices;
        [SerializeField] [Min(0.1f)] private float interactionDistance = 2.4f;
        [SerializeField] private Vector3 promptLocalPosition = new Vector3(0f, 3.1f, 0f);
        [SerializeField] private Vector3 promptScale = new Vector3(0.48f, 0.48f, 1f);
        [SerializeField] [Min(0.01f)] private float promptPopDuration = 0.22f;
        [SerializeField] [Min(0f)] private float promptPopOffset = 0.2f;
        [SerializeField] private Sprite promptSprite;

        private Transform player;
        private SpriteRenderer promptRenderer;
        private float nextPlayerSearchTime;
        private bool promptWanted;
        private float promptPop;
        private bool talking;

        public static bool IsTalking { get; private set; }

        public event Action<int> ChoiceChosen;

        public void ApplyDefaultsIfEmpty(string npcName, string spokenLine, string[] replyChoices)
        {
            if (string.IsNullOrWhiteSpace(speakerName) || speakerName == "NPC")
            {
                speakerName = npcName;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                line = spokenLine;
            }

            if (choices == null || choices.Length == 0)
            {
                choices = replyChoices;
            }
        }

        private void Awake()
        {
            CreatePrompt();
        }

        private void OnDisable()
        {
            CloseTalk();
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
            bool canTalk = isNear
                           && !SceneWarpTotem2D.IsBusy
                           && (session == null || session.CanInteract);

            if (talking && (!isNear || !DialogueBoxUI.IsOpen))
            {
                CloseTalk();
            }

            promptWanted = canTalk && !talking;
            TickPromptPop();

            if (!canTalk || talking || !UnityEngine.Input.GetKeyDown(KeyCode.F))
            {
                return;
            }

            OpenTalk();
        }

        public void OpenTalk()
        {
            talking = true;
            IsTalking = true;
            DialogueBoxUI.Ensure().Open(
                speakerName,
                line,
                choices,
                OnChoice);
        }

        public void CloseTalk()
        {
            talking = false;
            if (IsTalking)
            {
                IsTalking = false;
            }

            if (DialogueBoxUI.IsOpen)
            {
                DialogueBoxUI.Ensure().Close();
            }
        }

        private void OnChoice(int index)
        {
            talking = false;
            IsTalking = false;
            ChoiceChosen?.Invoke(index);
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
            string npcName,
            string spokenLine,
            string[] replyChoices,
            Sprite sprite)
        {
            speakerName = npcName;
            line = spokenLine;
            choices = replyChoices;
            promptSprite = sprite;
        }
#endif
    }
}
