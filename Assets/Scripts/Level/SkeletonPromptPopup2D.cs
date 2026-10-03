using Castlevania2D.Input;
using Castlevania2D.UI;
using UnityEngine;

namespace Castlevania2D.Level
{
    /// <summary>
    /// Opens the parchment hint once when the player stands on the ground line.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkeletonPromptPopup2D : MonoBehaviour
    {
        private const string HintId = "crane-key";
        private const string HintTitle = "Сбить ключ";
        private const string DefaultHintText =
            "На катушке журавля лежит ключ. Нажми W + ПКМ — закройся сверху и отбей камень, чтобы сбить ключ.";

        [Header("Ground Line")]
        [SerializeField] private Vector2 lineCenter = new Vector2(22.8f, -37.2f);
        [SerializeField] private float lineHalfWidth = 2.6f;
        [SerializeField] private float lineHalfHeight = 1.2f;

        [Header("Hint")]
        [SerializeField] [TextArea(3, 8)] private string hintText = DefaultHintText;

        [Header("Detection")]
        [SerializeField] private string playerObjectName = "Player_HeroKnight";

        private Transform playerRoot;

        private void Awake()
        {
            hintText = DefaultHintText;
            BindToCraneGround();
        }

        private void Start()
        {
            BindToCraneGround();
            CachePlayerRoot();
        }

        private void BindToCraneGround()
        {
            float craneX = 22.8f;
            GameObject crane = GameObject.Find("MechanismFrame");
            if (crane == null)
            {
                crane = GameObject.Find("LiftMechanism");
            }

            if (crane != null)
            {
                craneX = crane.transform.position.x;
            }

            lineCenter = new Vector2(craneX, -37.2f);
            lineHalfWidth = 2.6f;
            lineHalfHeight = 1.2f;
        }

        private void Update()
        {
            if (HintJournal.Contains(HintId) || HintScrollUI.IsOpen || DialogueBoxUI.IsOpen)
            {
                return;
            }

            if (GameplayInputLock.IsLocked)
            {
                return;
            }

            if (playerRoot == null)
            {
                CachePlayerRoot();
            }

            if (playerRoot == null)
            {
                return;
            }

            Vector2 playerPosition = playerRoot.position;
            bool inZone = Mathf.Abs(playerPosition.x - lineCenter.x) <= lineHalfWidth
                          && Mathf.Abs(playerPosition.y - lineCenter.y) <= lineHalfHeight;
            if (!inZone)
            {
                return;
            }

            HintScrollUI.TryShow(
                HintId,
                HintTitle,
                string.IsNullOrWhiteSpace(hintText) ? DefaultHintText : hintText);
        }

        private void CachePlayerRoot()
        {
            if (playerRoot != null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(playerObjectName))
            {
                GameObject playerObject = GameObject.Find(playerObjectName);
                if (playerObject != null)
                {
                    playerRoot = playerObject.transform;
                    return;
                }
            }

            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
            {
                playerRoot = taggedPlayer.transform.root;
            }
        }
    }
}
