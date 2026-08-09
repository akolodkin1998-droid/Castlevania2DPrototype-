using UnityEngine;
using UnityEngine.Serialization;
using EnemyHealth = Castlevania2D.Health.Health;

namespace Castlevania2D.Enemies
{
    /// <summary>
    /// Places two world-fixed portals when the wizard starts its attack sequence.
    /// Portals animate and spawn Ent + Mushomor on a shared cooldown.
    /// <see cref="SealPortals"/> permanently stops spawns and hides both portals.
    /// Edit portal world positions on the wizard in the Inspector.
    /// </summary>
    public sealed class WizardPortalSpawner2D : MonoBehaviour
    {
        [Header("Portal Frames")]
        [SerializeField] private Sprite[] portalFrames = System.Array.Empty<Sprite>();
        [SerializeField] private float portalFrameRate = 12f;
        [SerializeField] private float portalScale = 1f;
        [SerializeField] private int portalSortingOrder = 4;

        [Header("Portal Placement (World Space)")]
        [Tooltip("World-space position of portal 1. Select Enemy_Wizard to edit / see gizmos.")]
        [FormerlySerializedAs("portal1WorldPosition")]
        [SerializeField] private Vector2 portal1Position = new Vector2(50.76f, -37.16f);

        [Tooltip("World-space position of portal 2. Select Enemy_Wizard to edit / see gizmos.")]
        [FormerlySerializedAs("portal2WorldPosition")]
        [SerializeField] private Vector2 portal2Position = new Vector2(76.5f, -37.16f);

        [Header("Summon Spawn")]
        [SerializeField] private GameObject entPrefab;
        [SerializeField] private GameObject mushomorPrefab;
        [SerializeField] private float entSpawnInterval = 10f;
        [FormerlySerializedAs("maxAlivePerPortal")]
        [SerializeField] private int maxEnts = 1;
        [SerializeField] private int maxMushomors = 1;
        [SerializeField] private bool spawnEntImmediately = true;
        [SerializeField] private Vector2 entSpawnOffset = Vector2.zero;

        [Header("Refs")]
        [SerializeField] private WizardAttackAnimator2D attackAnimator;
        [SerializeField] private Transform portalRoot;

        private EnemyHealth health;
        private bool portalsPlaced;
        private WizardPortal2D portal1;
        private WizardPortal2D portal2;

        public bool PortalsPlaced => portalsPlaced;
        public Vector2 Portal1Position => portal1Position;
        public Vector2 Portal2Position => portal2Position;

        public void EditorAssign(
            Sprite[] frames,
            GameObject ent,
            GameObject mushomor,
            float frameRate,
            float scale,
            float spawnInterval,
            int maxAliveEnts,
            int maxAliveMushomors,
            bool immediate)
        {
            portalFrames = frames ?? System.Array.Empty<Sprite>();
            entPrefab = ent;
            mushomorPrefab = mushomor;
            portalFrameRate = Mathf.Max(1f, frameRate);
            portalScale = Mathf.Max(0.01f, scale);
            entSpawnInterval = Mathf.Max(0.1f, spawnInterval);
            maxEnts = Mathf.Max(0, maxAliveEnts);
            maxMushomors = Mathf.Max(0, maxAliveMushomors);
            spawnEntImmediately = immediate;
        }

        public void EditorAssignPortalPositions(Vector2 portal1Pos, Vector2 portal2Pos)
        {
            portal1Position = portal1Pos;
            portal2Position = portal2Pos;
        }

        /// <summary>
        /// Permanently stops portal spawns and hides both portal GameObjects.
        /// Idempotent. Does not change wizard attack state — call
        /// <see cref="WizardAttackAnimator2D.ReturnToIdle"/> from the lever path.
        /// </summary>
        public void SealPortals()
        {
            permanentlySealed = true;

            if (portal1 == null || portal2 == null)
            {
                TryRecoverPortalRefs();
            }

            SealPortalInstance(ref portal1);
            SealPortalInstance(ref portal2);

            if (portalRoot != null && portalRoot.gameObject.activeSelf)
            {
                portalRoot.gameObject.SetActive(false);
            }
        }

        private static void SealPortalInstance(ref WizardPortal2D portal)
        {
            if (portal == null)
            {
                return;
            }

            portal.SealAndHide();
            portal = null;
        }

        /// <summary>Idempotent: places both portals once (safe to call from attack start / Attack1 end).</summary>
        public void SpawnPortals()
        {
            if (permanentlySealed || portalsPlaced)
            {
                return;
            }

            PlacePortals();
        }

        private bool permanentlySealed;

        private void TryRecoverPortalRefs()
        {
            if (portalRoot == null)
            {
                GameObject root = GameObject.Find("Wizard_Portals");
                if (root != null)
                {
                    portalRoot = root.transform;
                }
            }

            if (portalRoot != null)
            {
                if (portal1 == null)
                {
                    Transform t = portalRoot.Find("Wizard_Portal_1");
                    if (t != null)
                    {
                        portal1 = t.GetComponent<WizardPortal2D>();
                    }
                }

                if (portal2 == null)
                {
                    Transform t = portalRoot.Find("Wizard_Portal_2");
                    if (t != null)
                    {
                        portal2 = t.GetComponent<WizardPortal2D>();
                    }
                }
            }

            // Include inactive — Find() misses deactivated roots after a prior seal attempt.
            if (portal1 == null || portal2 == null)
            {
                WizardPortal2D[] portals = Object.FindObjectsByType<WizardPortal2D>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                for (int i = 0; i < portals.Length; i++)
                {
                    WizardPortal2D portal = portals[i];
                    if (portal == null)
                    {
                        continue;
                    }

                    if (portal1 == null && portal.name == "Wizard_Portal_1")
                    {
                        portal1 = portal;
                    }
                    else if (portal2 == null && portal.name == "Wizard_Portal_2")
                    {
                        portal2 = portal;
                    }
                }

                if (portal1 == null && portals.Length > 0)
                {
                    portal1 = portals[0];
                }

                if (portal2 == null && portals.Length > 1)
                {
                    portal2 = portals[1];
                }
            }
        }

        private void Awake()
        {
            if (attackAnimator == null)
            {
                attackAnimator = GetComponent<WizardAttackAnimator2D>();
            }

            health = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            BindAnimator();

            if (health != null)
            {
                health.Died += OnWizardDied;
            }
        }

        private void Start()
        {
            // Late bind if Awake/OnEnable ordering left the animator null.
            BindAnimator();
        }

        private void OnDisable()
        {
            if (attackAnimator != null)
            {
                attackAnimator.PortalsRequested -= OnPortalsRequested;
            }

            if (health != null)
            {
                health.Died -= OnWizardDied;
            }
        }

        private void OnDestroy()
        {
            // Do not destroy portals with the wizard — they stay as world props.
            portal1 = null;
            portal2 = null;
            portalRoot = null;
        }

        private void BindAnimator()
        {
            if (attackAnimator == null)
            {
                attackAnimator = GetComponent<WizardAttackAnimator2D>();
            }

            if (attackAnimator == null)
            {
                return;
            }

            attackAnimator.PortalsRequested -= OnPortalsRequested;
            attackAnimator.PortalsRequested += OnPortalsRequested;
        }

        private void OnPortalsRequested()
        {
            SpawnPortals();
        }

        private void OnWizardDied()
        {
            if (portal1 != null)
            {
                portal1.SetSpawningEnabled(false);
            }

            if (portal2 != null)
            {
                portal2.SetSpawningEnabled(false);
            }
        }

        private void PlacePortals()
        {
            if (portalFrames == null || portalFrames.Length == 0)
            {
                Debug.LogWarning("WizardPortalSpawner2D: no portal frames — cannot place portals.", this);
                return;
            }

            if (entPrefab == null)
            {
                Debug.LogWarning(
                    "WizardPortalSpawner2D: entPrefab is missing — portals will animate but Ents will not spawn.",
                    this);
            }

            if (mushomorPrefab == null)
            {
                Debug.LogWarning(
                    "WizardPortalSpawner2D: mushomorPrefab is missing — portals will animate but Mushomors will not spawn.",
                    this);
            }

            EnsurePortalRoot();
            portal1 = CreatePortal("Wizard_Portal_1", portal1Position);
            portal2 = CreatePortal("Wizard_Portal_2", portal2Position);
            portalsPlaced = true;

            Debug.Log(
                $"WizardPortalSpawner2D: portals placed at {portal1Position} and {portal2Position}.",
                this);
        }

        private void EnsurePortalRoot()
        {
            if (portalRoot == null)
            {
                var existing = GameObject.Find("Wizard_Portals");
                if (existing != null)
                {
                    portalRoot = existing.transform;
                }
                else
                {
                    var rootGo = new GameObject("Wizard_Portals");
                    portalRoot = rootGo.transform;
                }
            }

            // Always keep as a scene-root container so wizard scale/parent never skews portal world coords.
            portalRoot.SetParent(null, worldPositionStays: true);
            portalRoot.position = Vector3.zero;
            portalRoot.rotation = Quaternion.identity;
            portalRoot.localScale = Vector3.one;
        }

        private WizardPortal2D CreatePortal(string name, Vector2 worldPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(portalRoot, worldPositionStays: false);
            go.transform.position = new Vector3(worldPosition.x, worldPosition.y, 0f);
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = new Vector3(portalScale, portalScale, 1f);
            go.SetActive(true);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = portalSortingOrder;
            renderer.color = Color.white;
            if (portalFrames.Length > 0 && portalFrames[0] != null)
            {
                renderer.sprite = portalFrames[0];
            }

            var portal = go.AddComponent<WizardPortal2D>();
            portal.Configure(
                portalFrames,
                entPrefab,
                mushomorPrefab,
                portalFrameRate,
                entSpawnInterval,
                maxEnts,
                maxMushomors,
                spawnEntImmediately,
                entSpawnOffset,
                null);

            return portal;
        }

        private void OnDrawGizmosSelected()
        {
            DrawPortalGizmo(portal1Position);
            DrawPortalGizmo(portal2Position);
        }

        private static void DrawPortalGizmo(Vector2 worldPosition)
        {
            Vector3 pos = new Vector3(worldPosition.x, worldPosition.y, 0f);
            Gizmos.color = new Color(0.55f, 0.2f, 0.95f, 0.9f);
            Gizmos.DrawWireSphere(pos, 0.55f);
            Gizmos.DrawLine(pos + Vector3.left * 0.35f, pos + Vector3.right * 0.35f);
            Gizmos.DrawLine(pos + Vector3.down * 0.35f, pos + Vector3.up * 0.35f);
        }
    }
}
