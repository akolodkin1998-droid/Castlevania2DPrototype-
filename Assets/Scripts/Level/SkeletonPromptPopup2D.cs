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
        private const string HintId = "skeleton";
        private const string HintTitle = "Отбить предмет";
        private const string DefaultHintText =
            "Иногда нужно отбить предмет. Нажми W + ПКМ — так можно закрыться от камня сверху и забросить его в корзину.";

        [Header("Ground Line")]
        [SerializeField] private Vector2 lineCenter = new Vector2(19.7f, -37.2f);
        [SerializeField] private float lineHalfWidth = 1.4f;
        [SerializeField] private float lineHalfHeight = 0.6f;

        [Header("Hint")]
        [SerializeField] [TextArea(3, 8)] private string hintText = DefaultHintText;

        [Header("Detection")]
        [SerializeField] private string playerObjectName = "Player_HeroKnight";

        private Transform playerRoot;

        private void Start()
        {
            CachePlayerRoot();
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
