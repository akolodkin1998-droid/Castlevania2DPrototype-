using System.Collections;
using Castlevania2D.Player;
using Castlevania2D.UI;
using UnityEngine;

namespace Castlevania2D.Hub
{
    public sealed class HubRoomTransition2D : MonoBehaviour
    {
        private const string PlayerObjectName = "Player_HeroKnight";
        private const float InteriorPlayerScale = 2.16f;

        [SerializeField] [Min(0.05f)] private float fadeDuration = 0.45f;
        [SerializeField] private Transform player;
        [SerializeField] private CameraFollow2D cameraFollow;
        [SerializeField] private Rigidbody2D playerBody;
        [SerializeField] private Transform interiorSpawn;
        [SerializeField] private HubRoomView2D interiorView;

        private ScreenFadeOverlay fadeOverlay;
        private bool busy;
        private bool hasVillageScale;
        private Vector3 villagePlayerScale = Vector3.one;

        public bool IsBusy => busy;

        private void Awake()
        {
            fadeOverlay = ScreenFadeOverlay.Create(transform);
            CachePlayer();
        }

        private void Start()
        {
            CachePlayer();
            if (HubTaflSession.ConsumeReturnToShop())
            {
                HubTaflSession.ApplyCoinsToPlayer();
                ResumeInsideShop();
            }
        }

        public void TravelTo(Vector3 destination)
        {
            TravelTo(destination, null, false);
        }

        public void TravelTo(Vector3 destination, HubRoomView2D roomView)
        {
            TravelTo(destination, roomView, roomView != null);
        }

        public void TravelTo(Vector3 destination, HubRoomView2D roomView, bool enlargePlayer)
        {
            if (busy)
            {
                return;
            }

            StartCoroutine(TravelRoutine(destination, roomView, enlargePlayer));
        }

        private IEnumerator TravelRoutine(
            Vector3 destination,
            HubRoomView2D roomView,
            bool enlargePlayer)
        {
            busy = true;
            CachePlayer();
            if (player == null)
            {
                busy = false;
                yield break;
            }

            yield return fadeOverlay.FadeTo(1f, fadeDuration);
            PlacePlayer(destination);
            ApplyPlayerScale(enlargePlayer);
            if (cameraFollow != null)
            {
                if (roomView != null)
                {
                    roomView.Apply(cameraFollow);
                }
                else
                {
                    cameraFollow.ExitLockedView();
                }

                cameraFollow.SnapNow();
            }

            yield return fadeOverlay.FadeTo(0f, fadeDuration);
            busy = false;
        }

        private void ApplyPlayerScale(bool interior)
        {
            if (player == null)
            {
                return;
            }

            if (interior)
            {
                if (!hasVillageScale)
                {
                    villagePlayerScale = player.localScale;
                    hasVillageScale = true;
                }

                player.localScale = villagePlayerScale * InteriorPlayerScale;
                return;
            }

            player.localScale = hasVillageScale ? villagePlayerScale : Vector3.one;
            hasVillageScale = false;
        }

        private void PlacePlayer(Vector3 worldPosition)
        {
            if (playerBody != null)
            {
                playerBody.linearVelocity = Vector2.zero;
                playerBody.position = worldPosition;
            }

            player.position = worldPosition;
        }

        private void ResumeInsideShop()
        {
            CachePlayer();
            Transform spawn = interiorSpawn;
            HubRoomView2D roomView = interiorView;
            if (spawn == null && roomView == null)
            {
                roomView = FindFirstObjectByType<HubRoomView2D>();
            }

            if (spawn == null && roomView != null)
            {
                Transform marker = roomView.transform.Find("InteriorSpawn");
                if (marker != null)
                {
                    spawn = marker;
                }
            }

            if (spawn == null || player == null)
            {
                return;
            }

            PlacePlayer(spawn.position);
            ApplyPlayerScale(true);
            if (roomView == null)
            {
                roomView = spawn.GetComponentInParent<HubRoomView2D>();
            }

            if (cameraFollow != null && roomView != null)
            {
                roomView.Apply(cameraFollow);
                cameraFollow.SnapNow();
            }
        }

        private void CachePlayer()
        {
            if (player == null)
            {
                GameObject playerObject = GameObject.Find(PlayerObjectName);
                if (playerObject != null)
                {
                    player = playerObject.transform;
                }
            }

            if (player != null && playerBody == null)
            {
                playerBody = player.GetComponent<Rigidbody2D>();
            }

            if (cameraFollow == null && Camera.main != null)
            {
                cameraFollow = Camera.main.GetComponent<CameraFollow2D>();
            }
        }

#if UNITY_EDITOR
        public void EditorAssignInterior(Transform spawn, HubRoomView2D roomView)
        {
            interiorSpawn = spawn;
            interiorView = roomView;
        }
#endif
    }
}
