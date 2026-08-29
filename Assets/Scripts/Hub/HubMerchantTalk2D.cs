using Castlevania2D.UI;
using UnityEngine;

namespace Castlevania2D.Hub
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NpcTalk2D))]
    public sealed class HubMerchantTalk2D : MonoBehaviour
    {
        [SerializeField] private HubRoomTransition2D transition;
        [SerializeField] private Transform leaveShopDestination;

        private NpcTalk2D talk;

        public static bool BlocksHubInteract => NpcTalk2D.IsTalking || DialogueBoxUI.IsOpen;

        private void Awake()
        {
            talk = GetComponent<NpcTalk2D>();
            talk.ApplyDefaultsIfEmpty(
                "Варяг",
                "Заходи, путник. Кости не врут — сыграем в тафл, или ты лишь погреться зашёл?",
                new[] { "Давай сыграем.", "Мне пора идти." });
            talk.ChoiceChosen += OnChoice;
        }

        private void OnDestroy()
        {
            if (talk != null)
            {
                talk.ChoiceChosen -= OnChoice;
            }
        }

        private void OnChoice(int index)
        {
            if (index == 0)
            {
                HubSceneFadeLoad.Load(HubTaflSession.TaflSceneName);
                return;
            }

            if (transition == null || leaveShopDestination == null)
            {
                return;
            }

            transition.TravelTo(leaveShopDestination.position, null, false);
        }

#if UNITY_EDITOR
        public void EditorAssign(
            HubRoomTransition2D roomTransition,
            Transform leaveDestination,
            Sprite promptSprite)
        {
            transition = roomTransition;
            leaveShopDestination = leaveDestination;

            NpcTalk2D npcTalk = GetComponent<NpcTalk2D>();
            if (npcTalk == null)
            {
                npcTalk = gameObject.AddComponent<NpcTalk2D>();
            }

            npcTalk.EditorAssign(
                "Варяг",
                "Заходи, путник. Кости не врут — сыграем в тафл, или ты лишь погреться зашёл?",
                new[] { "Давай сыграем.", "Мне пора идти." },
                promptSprite);
        }
#endif
    }
}
