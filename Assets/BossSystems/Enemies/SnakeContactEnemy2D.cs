using Castlevania2D.Combat;
using Castlevania2D.Health;
using UnityEngine;

/// <summary>
/// Stationary snake: idle sprite until the player overlaps its trigger,
/// then plays attack frames once (forward then reverse) and instantly kills the player.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public sealed class SnakeContactEnemy2D : MonoBehaviour
{
    [SerializeField] private Sprite idleSprite;
    [SerializeField] private Sprite[] attackSprites;
    [SerializeField] private float attackFrameRate = 12f;
    [SerializeField] private bool faceLeft = true;
    [SerializeField] private bool killPlayerOnContact = true;
    [SerializeField] private bool playAttackOnContact = true;
    [SerializeField] private bool loopAttackPingPong = false;

    private SpriteRenderer spriteRenderer;
    private Collider2D hitCollider;
    private Health health;
    private bool attacking;
    private bool hasKilledPlayer;
    private int attackFrameIndex;
    private int attackDirection = 1;
    private float frameTimer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        health = GetComponent<Health>();
        EnsureContactCapsule();

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = faceLeft;
            if (idleSprite != null)
            {
                spriteRenderer.sprite = idleSprite;
            }
        }
    }

    private void OnEnable()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (health != null)
        {
            health.Died += OnDied;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Died -= OnDied;
        }
    }

    private void OnDied()
    {
        // Belt-and-suspenders with Health.destroyOnDeath: hide immediately on death.
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!attacking || attackSprites == null || attackSprites.Length == 0)
        {
            return;
        }

        if (attackSprites.Length == 1)
        {
            spriteRenderer.sprite = attackSprites[0];
            return;
        }

        float frameDuration = 1f / Mathf.Max(1f, attackFrameRate);
        frameTimer += Time.deltaTime;
        while (frameTimer >= frameDuration && attacking)
        {
            frameTimer -= frameDuration;
            AdvanceAttackPingPong();
            if (!attacking)
            {
                break;
            }

            spriteRenderer.sprite = attackSprites[attackFrameIndex];
        }
    }

    private void AdvanceAttackPingPong()
    {
        attackFrameIndex += attackDirection;

        if (attackFrameIndex >= attackSprites.Length - 1)
        {
            attackFrameIndex = attackSprites.Length - 1;
            attackDirection = -1;
            return;
        }

        if (attackFrameIndex > 0)
        {
            return;
        }

        attackFrameIndex = 0;
        if (loopAttackPingPong)
        {
            attackDirection = 1;
            return;
        }

        // One forward+reverse pass completed → idle.
        attacking = false;
        if (idleSprite != null && spriteRenderer != null)
        {
            spriteRenderer.sprite = idleSprite;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryStrikePlayer(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // Catch cases where overlap already exists when the snake is enabled.
        TryStrikePlayer(other);
    }

    private void TryStrikePlayer(Collider2D other)
    {
        if (!IsPlayerBodyCapsule(other))
        {
            return;
        }

        Health playerHealth = ResolvePlayerHealth(other);
        if (playerHealth == null || !playerHealth.IsAlive)
        {
            return;
        }

        if (playAttackOnContact && !attacking && attackSprites != null && attackSprites.Length > 0)
        {
            BeginAttack();
        }

        if (killPlayerOnContact && !hasKilledPlayer)
        {
            hasKilledPlayer = true;
            playerHealth.Kill();
        }
    }

    private void BeginAttack()
    {
        attacking = true;
        attackFrameIndex = 0;
        attackDirection = 1;
        frameTimer = 0f;
        spriteRenderer.sprite = attackSprites[0];
    }

    private static bool IsPlayerBodyCapsule(Collider2D other)
    {
        if (other == null || other is not CapsuleCollider2D)
        {
            return false;
        }

        if (other.GetComponent<Hitbox2D>() != null)
        {
            return false;
        }

        string objectName = other.gameObject.name;
        if (objectName.IndexOf("Attack", System.StringComparison.OrdinalIgnoreCase) >= 0
            || objectName.IndexOf("Hitbox", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        return ResolvePlayerHealth(other) != null;
    }

    private void EnsureContactCapsule()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        CapsuleCollider2D capsule = GetComponent<CapsuleCollider2D>();
        if (capsule == null)
        {
            capsule = gameObject.AddComponent<CapsuleCollider2D>();
        }

        capsule.enabled = true;
        capsule.isTrigger = true;
        if (idleSprite != null)
        {
            Bounds bounds = idleSprite.bounds;
            capsule.size = bounds.size;
            capsule.offset = bounds.center;
            capsule.direction = CapsuleDirection2D.Vertical;
        }
        else if (box != null)
        {
            capsule.size = box.size;
            capsule.offset = box.offset;
            capsule.direction = CapsuleDirection2D.Vertical;
        }

        if (box != null)
        {
            box.enabled = false;
        }

        hitCollider = capsule;
    }

    private static Health ResolvePlayerHealth(Collider2D other)
    {
        HeroKnight hero = other.GetComponentInParent<HeroKnight>();
        if (hero != null)
        {
            return hero.GetComponent<Health>();
        }

        // Fallback: Health on a root named like the prototype player.
        Health health = other.GetComponentInParent<Health>();
        if (health == null)
        {
            return null;
        }

        Transform root = health.transform;
        string name = root.name;
        if (name.IndexOf("Hero", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Player", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return health;
        }

        return null;
    }

#if UNITY_EDITOR
    public void EditorAssignSprites(Sprite idle, Sprite[] attack)
    {
        idleSprite = idle;
        attackSprites = attack ?? System.Array.Empty<Sprite>();
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer != null && idleSprite != null)
        {
            spriteRenderer.sprite = idleSprite;
        }

        EnsureContactCapsule();
    }
#endif
}
