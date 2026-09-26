using Castlevania2D.Combat;
using Castlevania2D.Enemies;
using Castlevania2D.Hub;
using Castlevania2D.Input;
using Castlevania2D.Loot;
using Castlevania2D.Save;
using Castlevania2D.UI;
using UnityEngine;

namespace Castlevania2D.Npcs
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class StruchokNpc2D : MonoBehaviour
    {
        private const string PlayerObjectName = "Player_HeroKnight";
        private const string PromptResourcePath = "UI/InteractPrompt_F";
        private const float VisualScale = 1.25f;
        private const float BodyWidth = 0.32f;
        private const float BodyHeight = 0.64f;
        private const float MoveSkin = 0.03f;
        private const float UnstuckStep = 0.12f;
        private const int UnstuckMaxSteps = 18;
        private const float GroundNormalMin = 0.45f;
        private const float FallGravity = 22f;
        private const float MaxFallSpeed = 16f;
        private const int FallFlickerLimit = 2;
        private const float FallFlickerWindow = 0.4f;
        private const float FallSuppressDuration = 0.55f;
        private const float FallStartDelay = 0.18f;
        private const float GroundSeamProbe = 0.22f;
        private const float MoveDirLockDuration = 0.4f;
        private const float HuddleClearDelay = 0.35f;
        private static readonly RaycastHit2D[] MoveHits = new RaycastHit2D[16];
        private static readonly Collider2D[] OverlapHits = new Collider2D[16];
        private static readonly int Attack1 = Animator.StringToHash("Attack1");
        private static readonly int Attack2 = Animator.StringToHash("Attack2");
        private static readonly int Attack3 = Animator.StringToHash("Attack3");

        [SerializeField] private Sprite[] hideIdleFrames;
        [SerializeField] private Sprite[] followIdleFrames;
        [SerializeField] private Sprite[] followWalkFrames;
        [SerializeField] private Sprite[] lootPickupFrames;
        [SerializeField] private Sprite[] jumpFrames;
        [SerializeField] private Sprite[] appearVfxFrames;
        [SerializeField] [Min(0.25f)] private float hideIdleRate = 2f;
        [SerializeField] [Min(0.25f)] private float followIdleRate = 8f;
        [SerializeField] [Min(0.25f)] private float followWalkRate = 16f;
        [SerializeField] [Min(0.25f)] private float lootPickupRate = 20f;
        [SerializeField] [Min(0.25f)] private float jumpDuration = 0.55f;
        [SerializeField] [Min(0.2f)] private float jumpHeight = 1.35f;
        [SerializeField] [Min(0.05f)] private float minAirTimeToCopyJump = 0.12f;
        [SerializeField] [Min(0.25f)] private float vfxRate = 12f;
        [SerializeField] [Min(0.1f)] private float interactionDistance = 1.6f;
        [SerializeField] private Vector3 promptLocalPosition = new Vector3(0f, 1.15f, 0f);
        [SerializeField] private Vector3 promptScale = new Vector3(0.48f, 0.48f, 1f);
        [SerializeField] [Min(0.1f)] private float followDistance = 1.8f;
        [SerializeField] [Min(0.05f)] private float slopeHuddleDistance = 0.4f;
        [SerializeField] [Min(0.1f)] private float followSpeed = 2.2f;
        [SerializeField] [Min(0.1f)] private float lootHuntSpeed = 8f;
        [SerializeField] [Min(0.1f)] private float combatLootSpeed = 8f;
        [SerializeField] [Min(0.1f)] private float combatLinger = 3.5f;
        [SerializeField] [Min(0.05f)] private float followStartDistance = 0.35f;
        [SerializeField] [Min(0.05f)] private float followStopDistance = 0.12f;

        private SpriteRenderer bodyRenderer;
        private SpriteRenderer vfxRenderer;
        private SpriteRenderer promptRenderer;
        private Transform player;
        private Animator playerAnimator;
        private SpriteRenderer playerSprite;
        private Rigidbody2D playerBody;
        private CapsuleCollider2D playerCapsule;
        private CompanionLootCollector2D lootCollector;
        private bool emerged;
        private bool following;
        private bool jumping;
        private bool falling;
        private float fallSpeed;
        private int fallFlickerCount;
        private float fallFlickerWindowStart;
        private float suppressFallUntil;
        private float noGroundTime;
        private float lastStandY;
        private float lockedMoveDir;
        private float moveDirLockUntil;
        private bool huddling;
        private float huddleClearTime;
        private float nextSeamJumpTime;
        private bool waitingForPlayerLanding;
        private bool wasPlayerGrounded = true;
        private float playerAirborneTime;
        private float jumpProgress;
        private Vector3 jumpStart;
        private Vector3 jumpLand;
        private bool jumpLandHasFloor;
        private bool pickingLoot;
        private bool settleInPlace;
        private int settleIdleSteps;
        private bool inCombat;
        private bool returningToPark;
        private float combatUntilTime;
        private float parkIdleReadyTime;
        private Vector3 combatParkPosition;
        private bool wasAttacking;
        private int bodyStep;
        private float bodyTimer;
        private int vfxStep = -1;
        private float vfxTimer;
        private float promptPop;
        private float nextPlayerSearchTime;

        public void AssignSets(
            Sprite[] hideFrames,
            Sprite[] followFrames,
            Sprite[] walkFrames,
            Sprite[] vfxFrames,
            Sprite[] pickupFrames = null,
            Sprite[] jumpSet = null)
        {
            hideIdleFrames = hideFrames;
            followIdleFrames = OffsetSpritesDown(followFrames, 20f);
            followWalkFrames = OffsetSpritesDown(walkFrames, 20f);
            appearVfxFrames = OffsetSpritesDown(vfxFrames, 20f);
            lootPickupFrames = OffsetSpritesDown(pickupFrames, 20f);
            jumpFrames = OffsetSpritesDown(jumpSet, 20f);
            followWalkRate = 16f;
            followIdleRate = 8f;
            lootPickupRate = 20f;
            lootHuntSpeed = 8f;
            combatLootSpeed = 8f;
            ApplyBodyFrame();
        }

        private void Awake()
        {
            bodyRenderer = GetComponent<SpriteRenderer>();
            transform.localScale = new Vector3(VisualScale, VisualScale, 1f);
            EnsureInvulnerable();
            DisableConflictingBehaviours();
            lootCollector = GetComponent<CompanionLootCollector2D>();
            if (lootCollector == null)
            {
                lootCollector = gameObject.AddComponent<CompanionLootCollector2D>();
            }

            CreateVfxChild();
            CreatePrompt();
            ApplyBodyFrame();
        }

        private void Update()
        {
            CachePlayerIfNeeded();
            if (!emerged)
            {
                TryPickup();
                TickBodyAnimation();
                TickVfx();
                TickPrompt();
                return;
            }

            TryEnterCombatFromAttack();
            TickCombatWindow();
            TickJumpFollow();
            if (!jumping)
            {
                TryJumpOverSlopeSeam();
            }

            if (!jumping)
            {
                TickFall();
            }

            if (!jumping && !falling && !waitingForPlayerLanding)
            {
                TickLootOrFollow();
            }

            TickBodyAnimation();
            TickVfx();
            TickPrompt();
        }

        private void EnsureInvulnerable()
        {
            Castlevania2D.Health.Health health = GetComponent<Castlevania2D.Health.Health>();
            if (health != null)
            {
                Destroy(health);
            }

            Collider2D[] colliders = GetComponents<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Destroy(colliders[i]);
            }

            Rigidbody2D body = GetComponent<Rigidbody2D>();
            if (body != null)
            {
                Destroy(body);
            }
        }

        private void DisableConflictingBehaviours()
        {
            PairLoopIdleSprite2D pairLoop = GetComponent<PairLoopIdleSprite2D>();
            if (pairLoop != null)
            {
                pairLoop.enabled = false;
            }

            NpcTalk2D talk = GetComponent<NpcTalk2D>();
            if (talk != null)
            {
                talk.enabled = false;
            }
        }

        private void CreateVfxChild()
        {
            Transform existing = transform.Find("AppearVfx");
            GameObject vfxObject = existing != null ? existing.gameObject : new GameObject("AppearVfx");
            if (existing == null)
            {
                vfxObject.transform.SetParent(transform, false);
            }

            vfxObject.transform.localPosition = Vector3.zero;
            vfxObject.transform.localScale = Vector3.one;
            vfxObject.transform.localRotation = Quaternion.identity;

            vfxRenderer = vfxObject.GetComponent<SpriteRenderer>();
            if (vfxRenderer == null)
            {
                vfxRenderer = vfxObject.AddComponent<SpriteRenderer>();
            }

            vfxRenderer.sortingOrder = bodyRenderer != null ? bodyRenderer.sortingOrder + 1 : 4;
            vfxRenderer.enabled = false;
        }

        private void CreatePrompt()
        {
            Transform existing = transform.Find("InteractionPrompt");
            GameObject promptObject = existing != null ? existing.gameObject : new GameObject("InteractionPrompt");
            if (existing == null)
            {
                promptObject.transform.SetParent(transform, false);
            }

            promptRenderer = promptObject.GetComponent<SpriteRenderer>();
            if (promptRenderer == null)
            {
                promptRenderer = promptObject.AddComponent<SpriteRenderer>();
            }

            if (promptRenderer.sprite == null)
            {
                promptRenderer.sprite = Resources.Load<Sprite>(PromptResourcePath);
            }

            promptRenderer.sortingOrder = 20;
            ApplyPromptVisual(0f);
        }

        private void TryPickup()
        {
            if (player == null || GameplayInputLock.IsLocked || DialogueBoxUI.IsOpen)
            {
                return;
            }

            SaveLoadSessionController session = SaveLoadSessionController.Instance;
            if (session != null && !session.CanInteract)
            {
                return;
            }

            if (((Vector2)(player.position - transform.position)).sqrMagnitude
                > interactionDistance * interactionDistance)
            {
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.F))
            {
                Emerge();
            }
        }

        private void TryEnterCombatFromAttack()
        {
            bool attacking = IsPlayerAttacking();
            if (attacking)
            {
                EnterCombatShelter();
            }

            wasAttacking = attacking;
        }

        private void TickCombatWindow()
        {
            if (!inCombat || pickingLoot || returningToPark)
            {
                return;
            }

            if (lootCollector != null && (lootCollector.HasTarget || lootCollector.HasAnyAvailable()))
            {
                return;
            }

            if (Time.time >= combatUntilTime)
            {
                LeaveCombatShelter();
            }
        }

        private void EnterCombatShelter()
        {
            combatUntilTime = Time.time + combatLinger;
            if (inCombat)
            {
                return;
            }

            inCombat = true;
            jumping = false;
            SetFalling(false);
            fallSpeed = 0f;
            waitingForPlayerLanding = false;
            settleInPlace = false;
            returningToPark = false;
            pickingLoot = false;
            following = false;
            combatParkPosition = transform.position;
            parkIdleReadyTime = Time.time;
            bodyStep = 0;
            bodyTimer = 0f;
            if (lootCollector != null)
            {
                lootCollector.ClearTarget();
            }

            PlantOnGround();
            combatParkPosition = transform.position;
            PlayVfx();
            ApplyBodyFrame();
        }

        private void LeaveCombatShelter()
        {
            inCombat = false;
            returningToPark = false;
            following = false;
            waitingForPlayerLanding = false;
            wasPlayerGrounded = IsPlayerGrounded();
            bodyStep = 0;
            bodyTimer = 0f;
            PlayVfx();
            ApplyBodyFrame();
        }

        private bool IsPlayerAttacking()
        {
            if (playerAnimator == null)
            {
                return false;
            }

            int current = playerAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            if (IsAttackHash(current))
            {
                return true;
            }

            return playerAnimator.IsInTransition(0)
                   && IsAttackHash(playerAnimator.GetNextAnimatorStateInfo(0).shortNameHash);
        }

        private static bool IsAttackHash(int hash)
        {
            return hash == Attack1 || hash == Attack2 || hash == Attack3;
        }

        private void Emerge()
        {
            emerged = true;
            following = false;
            BeginSettleInPlace();
            PlayVfx();
            ApplyBodyFrame();
        }

        private void BeginSettleInPlace()
        {
            settleInPlace = true;
            following = false;
            bodyStep = 0;
            bodyTimer = 0f;
            settleIdleSteps = followIdleFrames != null && followIdleFrames.Length > 0
                ? followIdleFrames.Length
                : 12;
        }

        private void TickJumpFollow()
        {
            if (player == null)
            {
                return;
            }

            if (IsCombatHiding())
            {
                float hideFeetY = ResolvePlayerFeetY(player.position);
                float hideDrop = transform.position.y - hideFeetY;
                if (hideDrop > 0.55f)
                {
                    if (!jumping)
                    {
                        BeginJumpTo(ResolveFallThroughPoint(player.position, hideFeetY));
                    }

                    if (jumping)
                    {
                        TickJumpArc();
                    }

                    return;
                }

                waitingForPlayerLanding = false;
                jumping = false;
                wasPlayerGrounded = IsPlayerGrounded();
                return;
            }

            bool grounded = IsPlayerGrounded();
            float playerFeetY = ResolvePlayerFeetY(player.position);
            float dropBelow = transform.position.y - playerFeetY;
            if (!jumping && dropBelow > 0.55f)
            {
                waitingForPlayerLanding = false;
                BeginJumpTo(ResolveFallThroughPoint(player.position, playerFeetY));
            }
            else if (wasPlayerGrounded && !grounded)
            {
                waitingForPlayerLanding = true;
                playerAirborneTime = 0f;
                following = false;
            }

            if (!grounded)
            {
                playerAirborneTime += Time.deltaTime;
            }

            if (waitingForPlayerLanding && grounded && !wasPlayerGrounded)
            {
                waitingForPlayerLanding = false;
                if (playerAirborneTime >= minAirTimeToCopyJump && !jumping)
                {
                    Vector3 land = ResolveLandPoint(player.position);
                    if (TryGetWalkableGroundY(land, 0.5f, 2.4f, out _))
                    {
                        BeginJumpTo(land);
                    }
                }
            }

            wasPlayerGrounded = grounded;

            if (jumping)
            {
                TickJumpArc();
            }
        }

        private void BeginJumpTo(Vector3 landPoint)
        {
            float startY = transform.position.y;
            if (TryGetWalkableGroundY(transform.position, 0.35f, 1.2f, out float groundedStartY))
            {
                startY = groundedStartY;
            }

            Vector3 desiredLand = landPoint;
            jumpLandHasFloor = true;
            if (!TryGetWalkableGroundY(desiredLand, 0.45f, 3.2f, out float landY)
                && !TryFindNearbyLandX(desiredLand, out desiredLand, out landY))
            {
                desiredLand = new Vector3(landPoint.x, startY, landPoint.z);
                landY = startY;
                jumpLandHasFloor = false;
            }

            jumpStart = new Vector3(transform.position.x, startY, transform.position.z);
            jumpLand = ResolveFreeStandPoint(new Vector3(desiredLand.x, landY, desiredLand.z));
            if (TryGetWalkableGroundY(jumpLand, 0.45f, 3.2f, out float confirmedLandY))
            {
                jumpLand.y = confirmedLandY;
                jumpLandHasFloor = true;
            }
            else
            {
                jumpLandHasFloor = false;
            }
            jumpProgress = 0f;
            jumping = true;
            SetFalling(false);
            fallSpeed = 0f;
            following = false;
            pickingLoot = false;
            settleInPlace = false;
            returningToPark = false;
            bodyStep = 0;
            bodyTimer = 0f;
            if (bodyRenderer != null)
            {
                bodyRenderer.flipX = jumpLand.x < jumpStart.x;
            }
        }

        private void TickJumpArc()
        {
            jumpProgress += Time.deltaTime / Mathf.Max(0.15f, jumpDuration);
            float t = Mathf.Clamp01(jumpProgress);
            Vector3 next = Vector3.Lerp(jumpStart, jumpLand, t);
            next.y += 4f * jumpHeight * t * (1f - t);

            if (t >= 0.45f && TryGetWalkableGroundY(next, 0.35f, 6f, out float groundY) && next.y <= groundY + 0.05f)
            {
                transform.position = new Vector3(next.x, groundY, next.z);
                FinishJumpLanding();
                return;
            }

            if (t >= 1f)
            {
                Vector3 end = new Vector3(jumpLand.x, jumpLandHasFloor ? jumpLand.y : next.y, transform.position.z);
                SetPositionBlocked(end);
                FinishJumpLanding();
                return;
            }

            SetPositionBlocked(next);
        }

        private void FinishJumpLanding()
        {
            jumping = false;
            following = false;
            bodyStep = 0;
            bodyTimer = 0f;
            if (!TryPlantIfWalkable(0.4f, 6f))
            {
                StartFall();
            }

            ResolveStuckOverlap();
            if (inCombat)
            {
                combatParkPosition = transform.position;
                PlayVfx();
            }
        }

        private bool IsPlayerGrounded()
        {
            if (playerAnimator != null)
            {
                return playerAnimator.GetBool("Grounded");
            }

            return playerBody == null || Mathf.Abs(playerBody.linearVelocity.y) < 0.2f;
        }

        private Vector3 ResolveLandPoint(Vector3 playerPosition)
        {
            float feetY = ResolvePlayerFeetY(playerPosition);
            Vector3 around = new Vector3(playerPosition.x, feetY, transform.position.z);
            if (TryGetWalkableGroundY(around, 0.5f, 2.4f, out float groundY))
            {
                around.y = groundY;
            }

            return around;
        }

        private Vector3 ResolveFallThroughPoint(Vector3 playerPosition, float playerFeetY)
        {
            Vector3 around = new Vector3(playerPosition.x, playerFeetY, transform.position.z);
            if (TryGetWalkableGroundY(around, 0.2f, 8f, out float groundY) && groundY <= playerFeetY + 0.2f)
            {
                around.y = groundY;
            }

            return around;
        }

        private float ResolvePlayerFeetY(Vector3 around)
        {
            if (playerCapsule != null && player != null)
            {
                float scaleY = Mathf.Abs(player.lossyScale.y);
                return player.position.y + (playerCapsule.offset.y * scaleY) - (playerCapsule.size.y * 0.5f * scaleY);
            }

            return around.y;
        }

        private bool IsIgnoredCollider(Collider2D hitCollider)
        {
            if (hitCollider == null || hitCollider.isTrigger)
            {
                return true;
            }

            Transform hitTransform = hitCollider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
            {
                return true;
            }

            if (player != null && (hitTransform == player || hitTransform.IsChildOf(player)))
            {
                return true;
            }

            if (EnemyCollisionPassThrough2D.IsCameraStopWall(hitCollider))
            {
                return true;
            }

            return hitCollider.GetComponentInParent<LootPickup2D>() != null
                   || hitCollider.GetComponentInParent<Hitbox2D>() != null
                   || hitCollider.GetComponentInParent<EnemyProjectile2D>() != null;
        }

        private bool IsActorCollider(Collider2D hitCollider)
        {
            if (hitCollider == null)
            {
                return false;
            }

            if (EnemyCollisionPassThrough2D.IsEnemyBody(hitCollider))
            {
                return true;
            }

            Transform root = hitCollider.transform.root;
            if (root != null && root.name.StartsWith("Enemy_", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (hitCollider.GetComponentInParent<Castlevania2D.Health.Health>() != null)
            {
                return true;
            }

            return hitCollider.GetComponentInParent<ContactDamage2D>() != null
                   || hitCollider.GetComponentInParent<StruchokNpc2D>() != null;
        }

        private bool IsWalkableFloor(RaycastHit2D hit)
        {
            return !IsIgnoredCollider(hit.collider)
                   && !IsActorCollider(hit.collider)
                   && hit.normal.y >= GroundNormalMin;
        }

        private bool IsBlockingGeometry(Collider2D hitCollider)
        {
            return !IsIgnoredCollider(hitCollider) && !IsActorCollider(hitCollider);
        }

        private void TickFall()
        {
            Vector3 current = transform.position;
            if (TryGetStableGroundY(current, 0.35f, 0.35f, out float standY))
            {
                noGroundTime = 0f;
                lastStandY = standY;
                SetFalling(false);
                if (Mathf.Abs(current.y - standY) <= 0.14f)
                {
                    current.y = standY;
                    transform.position = current;
                }

                ResolveStuckOverlap();
                return;
            }

            noGroundTime += Time.deltaTime;
            if (noGroundTime < FallStartDelay || !CanStartFall())
            {
                SetFalling(false);
                return;
            }

            SetFalling(true);
            fallSpeed = Mathf.Min(MaxFallSpeed, fallSpeed + (FallGravity * Time.deltaTime));
            float nextY = current.y - (fallSpeed * Time.deltaTime);
            float search = Mathf.Max(0.55f, current.y - nextY + 0.4f);
            if (TryGetWalkableGroundY(current, 0.35f, search, out float floorY) && nextY <= floorY + 0.01f)
            {
                current.y = floorY;
                transform.position = current;
                SetFalling(false);
                ResolveStuckOverlap();
                return;
            }

            SetPositionBlocked(new Vector3(current.x, nextY, current.z));
        }

        private void StartFall()
        {
            if (!CanStartFall())
            {
                return;
            }

            SetFalling(true);
            if (fallSpeed < 2f)
            {
                fallSpeed = 2f;
            }
        }

        private bool CanStartFall()
        {
            return Time.time >= suppressFallUntil;
        }

        private void SetFalling(bool value)
        {
            if (value && !CanStartFall())
            {
                return;
            }

            if (falling == value)
            {
                return;
            }

            if (falling && !value)
            {
                RegisterFallFlicker();
            }

            falling = value;
            if (!value)
            {
                fallSpeed = 0f;
            }
        }

        private void RegisterFallFlicker()
        {
            if (Time.time - fallFlickerWindowStart > FallFlickerWindow)
            {
                fallFlickerWindowStart = Time.time;
                fallFlickerCount = 0;
            }

            fallFlickerCount++;
            if (fallFlickerCount >= FallFlickerLimit)
            {
                suppressFallUntil = Time.time + FallSuppressDuration;
                fallFlickerCount = 0;
            }
        }

        private bool TryPlantIfWalkable(float maxRise, float maxDrop)
        {
            Vector3 planted = transform.position;
            if (!TryGetWalkableGroundY(planted, maxRise, maxDrop, out float groundY))
            {
                return false;
            }

            planted.y = groundY;
            transform.position = planted;
            SetFalling(false);
            return true;
        }

        private void PlantOnGround()
        {
            if (!TryPlantIfWalkable(0.35f, 1.2f))
            {
                StartFall();
            }

            ResolveStuckOverlap();
        }

        private void SetPositionBlocked(Vector3 desired)
        {
            Vector3 current = transform.position;
            float x = current.x;
            float y = current.y;
            float deltaX = desired.x - current.x;
            if (Mathf.Abs(deltaX) > 0.0001f)
            {
                float direction = Mathf.Sign(deltaX);
                float allowed = CastAllowed(current, new Vector2(direction, 0f), Mathf.Abs(deltaX), true);
                x = current.x + (direction * allowed);
            }

            Vector3 afterX = new Vector3(x, current.y, current.z);
            float deltaY = desired.y - current.y;
            if (Mathf.Abs(deltaY) > 0.0001f)
            {
                float direction = Mathf.Sign(deltaY);
                float allowed = CastAllowed(afterX, new Vector2(0f, direction), Mathf.Abs(deltaY), false);
                y = current.y + (direction * allowed);
            }

            transform.position = new Vector3(x, y, current.z);
            if (!jumping && !falling)
            {
                TryPlantIfWalkable(0.28f, 0.35f);
            }

            ResolveStuckOverlap();
        }

        private bool IsBodyOverlapping(Vector3 at)
        {
            Vector2 origin = new Vector2(at.x, at.y + (BodyHeight * 0.5f) + 0.05f);
            Vector2 size = new Vector2(BodyWidth * 0.9f, BodyHeight * 0.7f);
            int hitCount = Physics2D.OverlapBox(origin, size, 0f, SolidFilter(), OverlapHits);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = OverlapHits[i];
                if (!IsBlockingGeometry(hit) || hit.bounds.max.y <= at.y + 0.08f)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private Vector3 ResolveFreeStandPoint(Vector3 desired)
        {
            Vector3 probe = desired;
            if (TryGetWalkableGroundY(probe, 0.45f, 3.2f, out float groundY))
            {
                probe.y = groundY;
                if (!IsBodyOverlapping(probe))
                {
                    return probe;
                }
            }

            if (TryFindNearbyLandX(desired, out Vector3 found, out _))
            {
                return found;
            }

            return desired;
        }

        private bool TryFindNearbyLandX(Vector3 around, out Vector3 land, out float landY)
        {
            for (int i = 1; i <= UnstuckMaxSteps; i++)
            {
                float offset = UnstuckStep * i;
                if (TryStandAt(around.x + offset, around, out land, out landY)
                    || TryStandAt(around.x - offset, around, out land, out landY))
                {
                    return true;
                }
            }

            land = around;
            landY = around.y;
            return false;
        }

        private bool TryStandAt(float x, Vector3 around, out Vector3 land, out float landY)
        {
            Vector3 probe = new Vector3(x, around.y, around.z);
            if (!TryGetWalkableGroundY(probe, 0.45f, 3.2f, out landY))
            {
                land = probe;
                return false;
            }

            land = new Vector3(x, landY, around.z);
            return !IsBodyOverlapping(land);
        }

        private void ResolveStuckOverlap()
        {
            if (!IsBodyOverlapping(transform.position))
            {
                return;
            }

            Vector3 start = transform.position;
            for (int i = 1; i <= UnstuckMaxSteps; i++)
            {
                float offset = UnstuckStep * i;
                if (TryMoveOutOfWall(start, offset) || TryMoveOutOfWall(start, -offset))
                {
                    return;
                }
            }
        }

        private bool TryMoveOutOfWall(Vector3 start, float offsetX)
        {
            Vector3 probe = new Vector3(start.x + offsetX, start.y, start.z);
            if (TryGetWalkableGroundY(probe, 0.35f, 1.6f, out float groundY))
            {
                probe.y = groundY;
            }

            if (IsBodyOverlapping(probe))
            {
                return false;
            }

            transform.position = probe;
            return true;
        }

        private static ContactFilter2D SolidFilter()
        {
            ContactFilter2D filter = new ContactFilter2D();
            filter.useTriggers = false;
            filter.useLayerMask = false;
            filter.useDepth = false;
            return filter;
        }

        private float CastAllowed(Vector3 from, Vector2 direction, float distance, bool horizontal)
        {
            Vector2 origin = new Vector2(from.x, from.y + (BodyHeight * 0.5f) + 0.05f);
            Vector2 size = new Vector2(BodyWidth, BodyHeight * 0.72f);
            int hitCount = Physics2D.BoxCast(
                origin,
                size,
                0f,
                direction,
                SolidFilter(),
                MoveHits,
                distance + MoveSkin);
            float allowed = distance;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = MoveHits[i];
                if (!IsBlockingGeometry(hit.collider) || hit.distance <= 0.0001f)
                {
                    continue;
                }

                if (horizontal)
                {
                    if (hit.normal.y >= 0.4f)
                    {
                        continue;
                    }
                }
                else if (direction.y < 0f)
                {
                    if (hit.normal.y >= 0.45f)
                    {
                        continue;
                    }
                }
                else if (direction.y > 0f && hit.normal.y > -0.35f)
                {
                    continue;
                }

                allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - MoveSkin));
            }

            return allowed;
        }

        private void TickLootOrFollow()
        {
            if (pickingLoot)
            {
                if (lootPickupFrames == null
                    || lootPickupFrames.Length == 0
                    || bodyStep >= lootPickupFrames.Length)
                {
                    FinishLootPickup();
                }

                return;
            }

            if (settleInPlace)
            {
                FollowPlayer();
                return;
            }

            if (inCombat)
            {
                TickCombatLootAndPark();
                return;
            }

            if (lootCollector != null)
            {
                lootCollector.RefreshTarget();
                if (lootCollector.HasTarget)
                {
                    HuntLoot(lootHuntSpeed);
                    return;
                }
            }

            FollowPlayer();
        }

        private void TickCombatLootAndPark()
        {
            if (lootCollector != null)
            {
                lootCollector.RefreshTarget();
                if (lootCollector.HasTarget)
                {
                    returningToPark = false;
                    HuntLoot(combatLootSpeed);
                    return;
                }
            }

            if (returningToPark)
            {
                if (MoveTowardX(combatParkPosition.x, combatLootSpeed))
                {
                    returningToPark = false;
                    following = false;
                    bodyStep = 0;
                    bodyTimer = 0f;
                    PlayVfx();
                    ApplyBodyFrame();
                }

                return;
            }

            following = false;
        }

        private void HuntLoot(float speed)
        {
            LootPickup2D loot = lootCollector.Target;
            if (loot == null)
            {
                return;
            }

            float targetX = loot.transform.position.x;
            float npcX = transform.position.x;
            if (lootCollector.IsInReach)
            {
                if (bodyRenderer != null)
                {
                    bodyRenderer.flipX = npcX > targetX;
                }

                pickingLoot = true;
                following = false;
                bodyStep = 0;
                bodyTimer = 0f;
                if (lootPickupFrames == null || lootPickupFrames.Length == 0)
                {
                    FinishLootPickup();
                }

                return;
            }

            MoveTowardX(targetX, speed);
        }

        private bool MoveTowardX(float targetX, float speed)
        {
            float npcX = transform.position.x;
            if (!following)
            {
                following = true;
                bodyStep = 0;
                bodyTimer = 0f;
            }

            MoveOnGroundTowardX(targetX, speed);
            if (bodyRenderer != null)
            {
                float moved = transform.position.x - npcX;
                bodyRenderer.flipX = Mathf.Abs(moved) > 0.0001f ? moved < 0f : npcX > targetX;
            }

            return Mathf.Abs(transform.position.x - targetX) <= followStopDistance;
        }

        private void FinishLootPickup()
        {
            if (lootCollector != null)
            {
                LootPickup2D loot = lootCollector.Target;
                if (loot != null)
                {
                    loot.TryCollectToPlayer();
                }

                lootCollector.ClearTarget();
            }

            pickingLoot = false;
            following = false;
            bodyStep = 0;
            bodyTimer = 0f;
            if (lootCollector != null)
            {
                lootCollector.RefreshTarget();
                if (lootCollector.HasTarget)
                {
                    combatUntilTime = Time.time + combatLinger;
                    return;
                }
            }

            if (inCombat)
            {
                returningToPark = true;
                combatUntilTime = Time.time + combatLinger;
            }
        }

        private void FollowPlayer()
        {
            if (player == null)
            {
                return;
            }

            if (settleInPlace)
            {
                if (bodyStep < settleIdleSteps)
                {
                    following = false;
                    return;
                }

                settleInPlace = false;
            }

            int playerFacing = ResolvePlayerFacing();
            float playerX = player.position.x;
            float npcX = transform.position.x;
            bool inFront = (npcX - playerX) * playerFacing > followStartDistance;
            float parkDistance = followDistance;
            if (UpdateHuddle(playerX, npcX))
            {
                parkDistance = slopeHuddleDistance;
                inFront = false;
            }

            float targetX = inFront
                ? playerX
                : playerX - (playerFacing * parkDistance);
            float remainX = Mathf.Abs(npcX - targetX);
            bool playerWalking = IsPlayerWalking();
            bool catchingUp = following
                ? remainX > followStopDistance
                : remainX > followStartDistance;
            bool playWalk = catchingUp || (playerWalking && !inFront);
            if (playWalk != following)
            {
                following = playWalk;
                bodyStep = 0;
                bodyTimer = 0f;
            }

            MoveOnGroundTowardX(targetX, followSpeed);

            if (bodyRenderer != null)
            {
                bodyRenderer.flipX = ResolveLookLeft(
                    playerFacing,
                    inFront,
                    npcX,
                    playerX,
                    transform.position.x - npcX);
            }
        }

        private void MoveOnGroundTowardX(float targetX, float speed)
        {
            Vector3 current = transform.position;
            if (!TryGetStableGroundY(current, 0.35f, 0.35f, out float standY))
            {
                return;
            }

            lastStandY = standY;
            if (!TryCommitMoveTarget(current.x, ref targetX))
            {
                return;
            }

            float destX = Mathf.MoveTowards(current.x, targetX, speed * Time.deltaTime);
            Vector3 dest = new Vector3(destX, standY, current.z);
            if (!TryGetStableGroundY(dest, 0.35f, 0.4f, out float destY))
            {
                SetPositionBlocked(new Vector3(current.x, standY, current.z));
                return;
            }

            dest.y = Mathf.MoveTowards(current.y, destY, 2.4f * Time.deltaTime);
            SetPositionBlocked(dest);
        }

        private void TryJumpOverSlopeSeam()
        {
            if (jumping
                || pickingLoot
                || settleInPlace
                || IsCombatHiding()
                || player == null
                || Time.time < nextSeamJumpTime)
            {
                return;
            }

            if (!IsAtSlopeSeam(transform.position))
            {
                return;
            }

            float dir = Mathf.Sign(player.position.x - transform.position.x);
            if (Mathf.Abs(dir) < 0.01f)
            {
                dir = 1f;
            }

            Vector3 land = new Vector3(
                transform.position.x + (dir * 1.15f),
                transform.position.y,
                transform.position.z);
            if (!TryGetWalkableGroundY(land, 0.6f, 2.2f, out _))
            {
                land = ResolveLandPoint(player.position);
            }

            nextSeamJumpTime = Time.time + 0.7f;
            lockedMoveDir = 0f;
            BeginJumpTo(land);
        }

        private bool IsAtSlopeSeam(Vector3 around)
        {
            bool hasSlope = false;
            bool hasFlat = false;
            for (int i = -2; i <= 2; i++)
            {
                Vector3 probe = around;
                probe.x += 0.2f * i;
                ClassifyGroundAt(probe, ref hasSlope, ref hasFlat);
                if (hasSlope && hasFlat)
                {
                    return true;
                }
            }

            return false;
        }

        private void ClassifyGroundAt(Vector3 around, ref bool hasSlope, ref bool hasFlat)
        {
            int hitCount = Physics2D.Raycast(
                new Vector2(around.x, around.y + 0.4f),
                Vector2.down,
                SolidFilter(),
                MoveHits,
                0.85f);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = MoveHits[i];
                if (IsIgnoredCollider(hit.collider) || IsActorCollider(hit.collider))
                {
                    continue;
                }

                if (hit.normal.y >= 0.82f)
                {
                    hasFlat = true;
                }
                else if (hit.normal.y >= 0.22f)
                {
                    hasSlope = true;
                }
            }
        }

        private bool TryCommitMoveTarget(float npcX, ref float targetX)
        {
            float desired = 0f;
            if (Mathf.Abs(targetX - npcX) > followStopDistance)
            {
                desired = Mathf.Sign(targetX - npcX);
            }

            if (desired != 0f
                && lockedMoveDir != 0f
                && desired != lockedMoveDir
                && Time.time < moveDirLockUntil)
            {
                return false;
            }

            if (desired != 0f && desired != lockedMoveDir)
            {
                lockedMoveDir = desired;
                moveDirLockUntil = Time.time + MoveDirLockDuration;
            }

            if (desired == 0f)
            {
                lockedMoveDir = 0f;
            }

            return true;
        }

        private bool UpdateHuddle(float playerX, float npcX)
        {
            if (ShouldHuddleNearPlayer(playerX, npcX))
            {
                huddling = true;
                huddleClearTime = Time.time + HuddleClearDelay;
                return true;
            }

            if (huddling && Time.time < huddleClearTime)
            {
                return true;
            }

            huddling = false;
            return false;
        }

        private bool ShouldHuddleNearPlayer(float playerX, float npcX)
        {
            if (player == null)
            {
                return false;
            }

            if (Mathf.Abs(npcX - playerX) > followDistance + 0.7f)
            {
                return false;
            }

            return IsNearSlope(player.position) || IsNearSlope(transform.position);
        }

        private bool IsNearSlope(Vector3 around)
        {
            Vector2 origin = new Vector2(around.x, around.y + 0.28f);
            Vector2 size = new Vector2(0.18f, 0.08f);
            Vector2[] directions = { Vector2.down, Vector2.left, Vector2.right };
            float[] distances = { 0.7f, 0.45f, 0.45f };
            for (int i = 0; i < directions.Length; i++)
            {
                int hitCount = Physics2D.BoxCast(
                    origin,
                    size,
                    0f,
                    directions[i],
                    SolidFilter(),
                    MoveHits,
                    distances[i]);
                for (int h = 0; h < hitCount; h++)
                {
                    RaycastHit2D hit = MoveHits[h];
                    if (IsIgnoredCollider(hit.collider) || IsActorCollider(hit.collider))
                    {
                        continue;
                    }

                    if (hit.normal.y >= 0.22f && hit.normal.y < 0.82f)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryGetStableGroundY(Vector3 around, float maxRise, float maxDrop, out float groundY)
        {
            float bestY = around.y;
            float bestDist = float.PositiveInfinity;
            bool found = false;
            for (int i = -1; i <= 1; i++)
            {
                Vector3 probe = around;
                probe.x += GroundSeamProbe * i;
                if (!TryGetWalkableGroundY(probe, maxRise, maxDrop, out float y))
                {
                    continue;
                }

                float dist = Mathf.Abs(y - around.y);
                if (dist >= bestDist)
                {
                    continue;
                }

                bestDist = dist;
                bestY = y;
                found = true;
            }

            groundY = bestY;
            return found;
        }

        private bool TryGetWalkableGroundY(Vector3 around, float maxRise, float maxDrop, out float groundY)
        {
            float probeY = around.y + maxRise + 0.12f;
            int hitCount = Physics2D.Raycast(
                new Vector2(around.x, probeY),
                Vector2.down,
                SolidFilter(),
                MoveHits,
                maxRise + maxDrop + 0.25f);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = MoveHits[i];
                if (!IsWalkableFloor(hit))
                {
                    continue;
                }

                float y = hit.point.y;
                if (y > around.y + maxRise || around.y - y > maxDrop)
                {
                    continue;
                }

                groundY = y;
                return true;
            }

            groundY = around.y;
            return false;
        }

        private static Sprite[] OffsetSpritesDown(Sprite[] source, float pixelsDown)
        {
            if (source == null || source.Length == 0)
            {
                return source;
            }

            var result = new Sprite[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                Sprite sprite = source[i];
                if (sprite == null)
                {
                    continue;
                }

                Vector2 pivotPixels = sprite.pivot;
                pivotPixels.y += pixelsDown;
                Vector2 pivotNormalized = new Vector2(
                    pivotPixels.x / sprite.rect.width,
                    pivotPixels.y / sprite.rect.height);
                Sprite shifted = Sprite.Create(
                    sprite.texture,
                    sprite.rect,
                    pivotNormalized,
                    sprite.pixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect);
                shifted.name = sprite.name;
                result[i] = shifted;
            }

            return result;
        }

        private static bool ResolveLookLeft(
            int playerFacing,
            bool inFront,
            float npcX,
            float playerX,
            float movedX)
        {
            if (Mathf.Abs(movedX) > 0.0001f)
            {
                return movedX < 0f;
            }

            if (inFront)
            {
                return npcX > playerX;
            }

            return playerFacing < 0;
        }

        private int ResolvePlayerFacing()
        {
            if (playerSprite != null)
            {
                return playerSprite.flipX ? -1 : 1;
            }

            return 1;
        }

        private bool IsPlayerWalking()
        {
            if (playerAnimator != null && playerAnimator.GetInteger("AnimState") == 1)
            {
                return true;
            }

            return playerBody != null && Mathf.Abs(playerBody.linearVelocity.x) > 0.15f;
        }

        private void TickBodyAnimation()
        {
            Sprite[] frames = ResolveActiveFrames();
            if (frames == null || frames.Length == 0)
            {
                return;
            }

            if (jumping || falling)
            {
                ApplyBodyFrame();
                return;
            }

            float rate = !emerged || IsCombatHiding()
                ? hideIdleRate
                : (pickingLoot ? lootPickupRate : (following ? followWalkRate : followIdleRate));
            bodyTimer += Time.deltaTime;
            float duration = 1f / Mathf.Max(0.25f, rate);
            while (bodyTimer >= duration)
            {
                bodyTimer -= duration;
                bodyStep++;
            }

            ApplyBodyFrame();
        }

        private void ApplyBodyFrame()
        {
            if (bodyRenderer == null)
            {
                return;
            }

            Sprite frame;
            if (!emerged || IsCombatHiding())
            {
                frame = ResolveHideFrame();
            }
            else if (jumping || falling)
            {
                frame = ResolveJumpFrame();
            }
            else if (pickingLoot)
            {
                frame = lootPickupFrames != null && lootPickupFrames.Length > 0
                    ? lootPickupFrames[Mathf.Min(bodyStep, lootPickupFrames.Length - 1)]
                    : ResolveFollowIdleFrame();
            }
            else if (following)
            {
                frame = ResolveWalkFrame();
            }
            else
            {
                frame = ResolveFollowIdleFrame();
            }

            if (frame != null)
            {
                bodyRenderer.sprite = frame;
            }
        }

        private Sprite ResolveHideFrame()
        {
            if (hideIdleFrames == null || hideIdleFrames.Length == 0)
            {
                return null;
            }

            const int pairSize = 2;
            const int pairPlayCount = 2;
            int pairCount = Mathf.Max(1, hideIdleFrames.Length / pairSize);
            int stepsPerPair = pairSize * pairPlayCount;
            int inCycle = bodyStep % (pairCount * stepsPerPair);
            int pair = inCycle / stepsPerPair;
            int within = inCycle % stepsPerPair;
            int index = (pair * pairSize) + (within % pairSize);
            if (index >= hideIdleFrames.Length)
            {
                index = hideIdleFrames.Length - 1;
            }

            return hideIdleFrames[index];
        }

        private Sprite[] ResolveActiveFrames()
        {
            if (!emerged || IsCombatHiding())
            {
                return hideIdleFrames;
            }

            if (jumping || falling)
            {
                return jumpFrames;
            }

            if (pickingLoot)
            {
                return lootPickupFrames;
            }

            return following ? followWalkFrames : followIdleFrames;
        }

        private Sprite ResolveJumpFrame()
        {
            if (jumpFrames == null || jumpFrames.Length == 0)
            {
                return ResolveFollowIdleFrame();
            }

            if (falling && !jumping)
            {
                return jumpFrames[Mathf.Min(6, jumpFrames.Length - 1)];
            }

            float t = Mathf.Clamp01(jumpProgress);
            int index;
            if (t < 0.22f)
            {
                index = Mathf.Clamp(Mathf.FloorToInt(t / 0.22f * 4f), 0, 3);
            }
            else if (t < 0.52f)
            {
                index = Mathf.Min(4, jumpFrames.Length - 1);
            }
            else if (t < 0.64f)
            {
                index = Mathf.Min(5, jumpFrames.Length - 1);
            }
            else if (t < 0.88f)
            {
                index = Mathf.Min(6, jumpFrames.Length - 1);
            }
            else
            {
                index = t < 0.95f
                    ? Mathf.Min(7, jumpFrames.Length - 1)
                    : Mathf.Min(8, jumpFrames.Length - 1);
            }

            return jumpFrames[index];
        }

        private Sprite ResolveWalkFrame()
        {
            if (followWalkFrames == null || followWalkFrames.Length == 0)
            {
                return ResolveFollowIdleFrame();
            }

            int index = bodyStep % followWalkFrames.Length;
            return followWalkFrames[index];
        }

        private Sprite ResolveFollowIdleFrame()
        {
            if (followIdleFrames == null || followIdleFrames.Length == 0)
            {
                return null;
            }

            int fullLength = followIdleFrames.Length;
            int loopStart = Mathf.Max(0, fullLength - 3);
            int loopLength = Mathf.Max(1, fullLength - loopStart);
            int loopCycles = 5;
            int loopSteps = loopLength * loopCycles;
            int cycleLength = fullLength + loopSteps;
            int inCycle = bodyStep % cycleLength;
            int index = inCycle < fullLength
                ? inCycle
                : loopStart + ((inCycle - fullLength) % loopLength);
            return followIdleFrames[index];
        }

        private bool IsCombatHiding()
        {
            return inCombat && !jumping && !falling && !pickingLoot && !following && !returningToPark;
        }

        private void PlayVfx()
        {
            if (appearVfxFrames == null || appearVfxFrames.Length == 0 || vfxRenderer == null)
            {
                return;
            }

            vfxStep = 0;
            vfxTimer = 0f;
            vfxRenderer.sprite = appearVfxFrames[0];
            vfxRenderer.flipX = bodyRenderer != null && bodyRenderer.flipX;
            vfxRenderer.enabled = true;
        }

        private void TickVfx()
        {
            if (vfxRenderer == null || vfxStep < 0 || appearVfxFrames == null || appearVfxFrames.Length == 0)
            {
                return;
            }

            vfxTimer += Time.deltaTime;
            float duration = 1f / Mathf.Max(0.25f, vfxRate);
            while (vfxTimer >= duration)
            {
                vfxTimer -= duration;
                vfxStep++;
                if (vfxStep >= appearVfxFrames.Length)
                {
                    vfxStep = -1;
                    vfxRenderer.enabled = false;
                    vfxRenderer.sprite = null;
                    return;
                }

                vfxRenderer.sprite = appearVfxFrames[vfxStep];
                vfxRenderer.flipX = bodyRenderer != null && bodyRenderer.flipX;
            }
        }

        private void TickPrompt()
        {
            bool show = !emerged && player != null
                        && ((Vector2)(player.position - transform.position)).sqrMagnitude
                        <= interactionDistance * interactionDistance
                        && !GameplayInputLock.IsLocked
                        && !DialogueBoxUI.IsOpen;
            float target = show ? 1f : 0f;
            promptPop = Mathf.MoveTowards(promptPop, target, 5f * Time.unscaledDeltaTime);
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
            promptTransform.localPosition = promptLocalPosition + new Vector3(0f, (eased - 1f) * 0.2f, 0f);
            Color color = Color.white;
            color.a = eased;
            promptRenderer.color = color;
            promptRenderer.enabled = eased > 0.001f && promptRenderer.sprite != null;
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
            playerAnimator = playerObject.GetComponent<Animator>();
            playerSprite = playerObject.GetComponent<SpriteRenderer>();
            playerBody = playerObject.GetComponent<Rigidbody2D>();
            playerCapsule = playerObject.GetComponent<CapsuleCollider2D>();
            wasPlayerGrounded = IsPlayerGrounded();
            waitingForPlayerLanding = !wasPlayerGrounded;
            playerAirborneTime = 0f;
            if (emerged)
            {
                BeginSettleInPlace();
            }
        }
    }
}
