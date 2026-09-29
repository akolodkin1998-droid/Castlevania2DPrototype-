using Castlevania2D.Input;
using UnityEngine;

namespace Castlevania2D.UI
{
    /// <summary>
    /// Opens the parchment hint once when the player is nearby, then stores it in the journal.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ParchmentHintTrigger2D : MonoBehaviour
    {
        [SerializeField] [Min(0.1f)] private float distance = 1.6f;
        [SerializeField] private string hintId;
        [SerializeField] private string hintTitle;
        [SerializeField] [TextArea(3, 8)] private string hintText;
        [SerializeField] private string playerObjectName = "Player_HeroKnight";
        [SerializeField] private bool armed = true;

        private Transform player;
        private float nextPlayerSearchTime;

        public void Configure(string id, string title, string text, float interactDistance)
        {
            hintId = id;
            hintTitle = title;
            hintText = text;
            distance = Mathf.Max(0.1f, interactDistance);
            armed = true;
        }

        public void SetArmed(bool value)
        {
            armed = value;
        }

        private void Update()
        {
            if (!armed || string.IsNullOrWhiteSpace(hintText) || string.IsNullOrEmpty(hintId))
            {
                return;
            }

            if (HintJournal.Contains(hintId) || HintScrollUI.IsOpen || DialogueBoxUI.IsOpen)
            {
                return;
            }

            if (GameplayInputLock.IsLocked)
            {
                return;
            }

            CachePlayer();
            if (player == null)
            {
                return;
            }

            if (((Vector2)(player.position - transform.position)).sqrMagnitude > distance * distance)
            {
                return;
            }

            HintScrollUI.TryShow(hintId, hintTitle, hintText);
        }

        private void CachePlayer()
        {
            if (player != null || Time.unscaledTime < nextPlayerSearchTime)
            {
                return;
            }

            nextPlayerSearchTime = Time.unscaledTime + 1f;
            if (string.IsNullOrEmpty(playerObjectName))
            {
                return;
            }

            GameObject playerObject = GameObject.Find(playerObjectName);
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }
    }
}
